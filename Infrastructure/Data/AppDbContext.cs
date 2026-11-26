using Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Country>      Countries      => Set<Country>();
    public DbSet<User>         Users          => Set<User>();
    public DbSet<Property>     Properties     => Set<Property>();
    public DbSet<PropertyMedia> PropertyMedia => Set<PropertyMedia>();
    public DbSet<Favourite>    Favourites     => Set<Favourite>();
    public DbSet<Conversation> Conversations  => Set<Conversation>();
    public DbSet<Message>      Messages       => Set<Message>();
    public DbSet<PriceHistory> PriceHistories => Set<PriceHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // ─── Country ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Country>(e =>
        {
            e.HasKey(x => x.CountryCode);
            e.Property(x => x.CountryCode).HasMaxLength(5);
            e.Property(x => x.CountryName).HasMaxLength(100).IsRequired();
            e.Property(x => x.CurrencyCode).HasMaxLength(5).IsRequired();
            e.Property(x => x.CurrencySymbol).HasMaxLength(10).IsRequired();

            // Seed data
            e.HasData(
                new Country { CountryCode = "GB", CountryName = "United Kingdom",       CurrencyCode = "GBP", CurrencySymbol = "£",    IsActive = true,  LaunchedAt = DateTime.UtcNow },
                new Country { CountryCode = "US", CountryName = "United States",        CurrencyCode = "USD", CurrencySymbol = "$",    IsActive = false, LaunchedAt = null },
                new Country { CountryCode = "AE", CountryName = "United Arab Emirates", CurrencyCode = "AED", CurrencySymbol = "د.إ", IsActive = false, LaunchedAt = null }
            );
        });

        // ─── User ─────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.CountryCode).HasMaxLength(5).IsRequired();
            e.Property(x => x.Role).HasConversion<string>();

            e.HasOne(x => x.Country)
             .WithMany(x => x.Users)
             .HasForeignKey(x => x.CountryCode)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Property ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Property>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Title).HasMaxLength(300).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.SuggestedPrice).HasPrecision(18, 2);
            e.Property(x => x.Latitude).HasPrecision(9, 6);
            e.Property(x => x.Longitude).HasPrecision(9, 6);
            e.Property(x => x.SizeValue).HasPrecision(10, 2);
            e.Property(x => x.CountryCode).HasMaxLength(5).IsRequired();
            e.Property(x => x.CurrencyCode).HasMaxLength(5).IsRequired();
            e.Property(x => x.Status).HasConversion<string>();
            e.Property(x => x.ListingType).HasConversion<string>();

            // ── Performance indexes (critical for search) ──
            e.HasIndex(x => x.CountryCode);                          // Always filter by country
            e.HasIndex(x => x.Region);
            e.HasIndex(x => x.Price);
            e.HasIndex(x => x.PropertyType);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.Bedrooms);
            e.HasIndex(x => x.ListingType);
            e.HasIndex(x => new { x.CountryCode, x.Status, x.Region }); // Composite for common queries

            e.HasOne(x => x.Seller)
             .WithMany(x => x.Properties)
             .HasForeignKey(x => x.SellerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Country)
             .WithMany(x => x.Properties)
             .HasForeignKey(x => x.CountryCode)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── PropertyMedia ────────────────────────────────────────────────────
        modelBuilder.Entity<PropertyMedia>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.Url).HasMaxLength(500).IsRequired();
            e.Property(x => x.PublicId).HasMaxLength(200).IsRequired();
            e.Property(x => x.MediaType).HasConversion<string>();

            e.HasOne(x => x.Property)
             .WithMany(x => x.Media)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Favourite ────────────────────────────────────────────────────────
        modelBuilder.Entity<Favourite>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UserId, x.PropertyId }).IsUnique();

            e.HasOne(x => x.User)
             .WithMany(x => x.Favourites)
             .HasForeignKey(x => x.UserId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Property)
             .WithMany(x => x.Favourites)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        // ─── Conversation ─────────────────────────────────────────────────────
        modelBuilder.Entity<Conversation>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.PropertyId, x.BuyerId }).IsUnique();
            e.HasIndex(x => x.BuyerId);
            e.HasIndex(x => x.SellerId);

            e.HasOne(x => x.Property)
             .WithMany(x => x.Conversations)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Buyer)
             .WithMany(x => x.BuyerConversations)
             .HasForeignKey(x => x.BuyerId)
             .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(x => x.Seller)
             .WithMany(x => x.SellerConversations)
             .HasForeignKey(x => x.SellerId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── Message ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Message>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ConversationId);  // Critical for chat performance
            e.HasIndex(x => new { x.ConversationId, x.SentAt });

            e.HasOne(x => x.Conversation)
             .WithMany(x => x.Messages)
             .HasForeignKey(x => x.ConversationId)
             .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Sender)
             .WithMany(x => x.Messages)
             .HasForeignKey(x => x.SenderId)
             .OnDelete(DeleteBehavior.Restrict);
        });

        // ─── PriceHistory ─────────────────────────────────────────────────────
        modelBuilder.Entity<PriceHistory>(e =>
        {
            e.HasKey(x => x.Id);
            e.Property(x => x.SuggestedPrice).HasPrecision(18, 2);
            e.Property(x => x.ActualPrice).HasPrecision(18, 2);

            e.HasOne(x => x.Property)
             .WithMany(x => x.PriceHistories)
             .HasForeignKey(x => x.PropertyId)
             .OnDelete(DeleteBehavior.Cascade);
        });
    }

    // Auto-update UpdatedAt on save
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Modified)
                entry.Entity.UpdatedAt = DateTime.UtcNow;
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}
