using System.Security.Claims;
using Infrastructure.AI;
using Infrastructure.Caching;
using Infrastructure.Data;
using Infrastructure.Data.Repositories;
using Infrastructure.Location;
using Infrastructure.Media;
using Core.Interfaces;
using Infrastructure.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using API.Hubs;
using API.Middleware;

var builder = WebApplication.CreateBuilder(args);

// ─── Database ─────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sql => sql.EnableRetryOnFailure(3)
    )
);

// ─── Repositories ─────────────────────────────────────────────────────────────
builder.Services.AddScoped<IPropertyRepository, PropertyRepository>();
builder.Services.AddScoped<IFavouriteRepository, FavouriteRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();

// ─── Services ─────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IPropertyService, PropertyService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IMediaService, CloudinaryMediaService>();
builder.Services.AddScoped<IAIPriceService, OpenAIPriceService>();
builder.Services.AddScoped<ILocationService, PostcodesIoLocationService>();

// ─── Cache — swap MemoryCacheService → RedisCacheService when ready ───────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ICacheService, MemoryCacheService>();
// When ready for Redis:
// builder.Services.AddStackExchangeRedisCache(o => o.Configuration = builder.Configuration["Redis:ConnectionString"]);
// builder.Services.AddSingleton<ICacheService, RedisCacheService>();

// ─── HTTP Clients ─────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("PostcodesIO", c =>
{
    c.BaseAddress = new Uri("https://api.postcodes.io/");
    c.Timeout     = TimeSpan.FromSeconds(5);
});
builder.Services.AddHttpClient("OpenAI", c =>
{
    c.Timeout = TimeSpan.FromSeconds(30);
});

// ─── Auth — JWT ───────────────────────────────────────────────────────────────
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key not configured");

// Console.WriteLine($"[JWT DEBUG] Key first 8 chars: {jwtKey[..8]}");


builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            ValidateIssuer           = true,
            ValidIssuer              = builder.Configuration["Jwt:Issuer"],
            ValidateAudience         = true,
            ValidAudience            = builder.Configuration["Jwt:Audience"],
            ValidateLifetime         = true,
            // Allow 5 min clock drift — prevents invalid_token on valid tokens
            ClockSkew                = TimeSpan.FromMinutes(5),
            // Map "role" claim correctly — ASP.NET remaps it by default which breaks our claim
            //RoleClaimType            = ClaimTypes.Role,
            //NameClaimType            = ClaimTypes.NameIdentifier
            RoleClaimType = "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
            NameClaimType = "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier"
        };

        // Allow JWT in SignalR query string
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var path = context.HttpContext.Request.Path;
                if (path.StartsWithSegments("/hubs"))
                {
                    var accessToken = context.Request.Query["access_token"].ToString();
                    if (!string.IsNullOrWhiteSpace(accessToken))
                        context.Token = accessToken;
                    else
                    {
                        var authHeader = context.Request.Headers["Authorization"].ToString();
                        if (authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                            context.Token = authHeader["Bearer ".Length..].Trim();
                    }
                }
                return Task.CompletedTask;
            },

            OnTokenValidated = context =>
            {
                // Console.WriteLine($"[JWT] ✅ Token VALID for: {context.Principal?.Identity?.Name}");
                return Task.CompletedTask;
            },

            OnAuthenticationFailed = context =>
            {
                // Console.WriteLine($"[JWT] ❌ Auth FAILED: {context.Exception.GetType().Name}");
                // Console.WriteLine($"[JWT] ❌ Reason: {context.Exception.Message}");
                if (context.Exception.InnerException != null)
                     Console.WriteLine($"[JWT] ❌ Inner: {context.Exception.InnerException.Message}");
                return Task.CompletedTask;
            },

            OnChallenge = context =>
            {
                // Console.WriteLine($"[JWT] ⚠️ Challenge triggered for: {context.Request.Path}");
                // Console.WriteLine($"[JWT] ⚠️ Error: {context.Error}");
                // Console.WriteLine($"[JWT] ⚠️ ErrorDescription: {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

// ─── SignalR ───────────────────────────────────────────────────────────────────
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;  // Shows exact hub errors in dev
});

// ─── CORS ─────────────────────────────────────────────────────────────────────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        if (builder.Environment.IsDevelopment())
        {
            policy.SetIsOriginAllowed(origin =>
                    origin.StartsWith("http://localhost") ||
                    origin.StartsWith("https://localhost"))
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.WithOrigins(builder.Configuration["Frontend:Url"] ?? "")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
    });
});

// ─── Controllers + Swagger ────────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "RealEstate API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new()
    {
        Name   = "Authorization",
        Type   = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new()
    {
        {
            new() { Reference = new() { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" } },
            []
        }
    });
});

var app = builder.Build();

// ─── Middleware Pipeline ───────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "RealEstate API v1");
    });
}

// Correct middleware order
if (!app.Environment.IsDevelopment())
    app.UseHttpsRedirection();
app.UseCors("AllowFrontend");           // CORS must be before Auth + custom middleware
app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGet("/hubs/test", () => "Hub route works");

app.MapHub<ChatHub>("/hubs/chat");

// ─── Auto-migrate on startup (dev only) ──────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    try
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Database migration failed. Check your connection string in appsettings.Development.json");
    }
}

app.Run();
