using Core.DTOs.Auth;
using Core.Entities;
using Core.Interfaces;
using Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly AppDbContext   _db;
    private readonly IConfiguration _config;

    public AuthService(AppDbContext db, IConfiguration config)
    {
        _db     = db;
        _config = config;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        // Check email already exists
        if (await _db.Users.AnyAsync(u => u.Email == request.Email.ToLower()))
            throw new InvalidOperationException("Email address already registered");

        // Validate country is active
        var country = await _db.Countries.FindAsync(request.CountryCode.ToUpper())
            ?? throw new ArgumentException($"Country '{request.CountryCode}' is not supported");

        if (!country.IsActive)
            throw new InvalidOperationException($"Service not yet available in {country.CountryName}");

        // Validate role
        if (!Enum.TryParse<UserRole>(request.Role, out var role))
            throw new ArgumentException("Role must be Buyer, Seller, or Both");

        var user = new User
        {
            Email        = request.Email.ToLower().Trim(),
            FullName     = request.FullName.Trim(),
            Phone        = request.Phone?.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role         = role,
            CountryCode  = request.CountryCode.ToUpper()
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        var token = await GenerateJwtTokenAsync(user);
        return BuildAuthResponse(user, token);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLower() && u.IsActive)
            ?? throw new UnauthorizedAccessException("Invalid email or password");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        var token = await GenerateJwtTokenAsync(user);
        return BuildAuthResponse(user, token);
    }

    public Task<string> GenerateJwtTokenAsync(User user)
    {
        var key    = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var creds  = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var expiry = DateTime.UtcNow.AddHours(int.Parse(_config["Jwt:ExpiryHours"] ?? "24"));

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email,          user.Email),
            new Claim(ClaimTypes.Name,           user.FullName),
            new Claim(ClaimTypes.Role,           user.Role.ToString()),
            new Claim("countryCode",             user.CountryCode),
        };

        var token = new JwtSecurityToken(
            issuer:             _config["Jwt:Issuer"],
            audience:           _config["Jwt:Audience"],
            claims:             claims,
            expires:            expiry,
            signingCredentials: creds
        );

        return Task.FromResult(new JwtSecurityTokenHandler().WriteToken(token));
    }

    private static AuthResponse BuildAuthResponse(User user, string token) => new()
    {
        Token        = token,
        RefreshToken = string.Empty,
        ExpiresAt    = DateTime.UtcNow.AddHours(24),
        User         = new UserDto
        {
            Id              = user.Id,
            Email           = user.Email,
            FullName        = user.FullName,
            Role            = user.Role.ToString(),
            CountryCode     = user.CountryCode,
            ProfileImageUrl = user.ProfileImageUrl,
            IsVerified      = user.IsVerified
        }
    };
}
