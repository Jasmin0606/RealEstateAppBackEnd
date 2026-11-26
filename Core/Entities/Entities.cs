namespace Core.Entities;

// ─── Base ────────────────────────────────────────────────────────────────────
public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

// ─── Country ─────────────────────────────────────────────────────────────────
public class Country
{
    public string CountryCode { get; set; } = string.Empty;   // PK: GB, US, AE
    public string CountryName { get; set; } = string.Empty;
    public string CurrencyCode { get; set; } = string.Empty;
    public string CurrencySymbol { get; set; } = string.Empty;
    public bool IsActive { get; set; } = false;
    public DateTime? LaunchedAt { get; set; }

    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<User> Users { get; set; } = [];
}

// ─── User ─────────────────────────────────────────────────────────────────────
public class User : BaseEntity
{
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Buyer;
    public string CountryCode { get; set; } = string.Empty;
    public bool IsVerified { get; set; } = false;
    public string? ProfileImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public Country? Country { get; set; }
    public ICollection<Property> Properties { get; set; } = [];
    public ICollection<Favourite> Favourites { get; set; } = [];
    public ICollection<Conversation> BuyerConversations { get; set; } = [];
    public ICollection<Conversation> SellerConversations { get; set; } = [];
    public ICollection<Message> Messages { get; set; } = [];
}

public enum UserRole { Buyer, Seller, Both, Admin }

// ─── Property ─────────────────────────────────────────────────────────────────
public class Property : BaseEntity
{
    public Guid SellerId { get; set; }
    public string CountryCode { get; set; } = string.Empty;

    // Universal fields
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public PropertyStatus Status { get; set; } = PropertyStatus.Active;
    public ListingType ListingType { get; set; } = ListingType.Sale;
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }  // sqft / sqm

    // Location
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? Region { get; set; }        // Borough / State / Emirate
    public string? PostalCode { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Country-specific fields as JSON
    // UK:  { "Tenure": "Leasehold", "EPCRating": "C", "CouncilTaxBand": "D", "NearestTube": "Angel", "TubeZone": "Zone 1" }
    // US:  { "HOAFee": 350, "PropertyTax": 4200 }
    // UAE: { "Emirate": "Dubai", "ServiceCharge": 15, "Furnished": "Fully Furnished" }
    public string? CountryAttributes { get; set; }  // JSON

    // AI price suggestion
    public decimal? SuggestedPrice { get; set; }
    public DateTime? SuggestedAt { get; set; }

    public int ViewCount { get; set; } = 0;
    public bool IsFeatured { get; set; } = false;

    // Navigation
    public User? Seller { get; set; }
    public Country? Country { get; set; }
    public ICollection<PropertyMedia> Media { get; set; } = [];
    public ICollection<Favourite> Favourites { get; set; } = [];
    public ICollection<Conversation> Conversations { get; set; } = [];
    public ICollection<PriceHistory> PriceHistories { get; set; } = [];
}

public enum PropertyStatus  { Active, UnderOffer, Sold, Withdrawn, Draft }
public enum ListingType     { Sale, Rent }

// ─── PropertyMedia ────────────────────────────────────────────────────────────
public class PropertyMedia : BaseEntity
{
    public Guid PropertyId { get; set; }
    public MediaType MediaType { get; set; }
    public string Url { get; set; } = string.Empty;         // Cloudinary URL
    public string PublicId { get; set; } = string.Empty;    // Cloudinary public ID
    public bool IsPrimary { get; set; } = false;
    public int DisplayOrder { get; set; } = 0;

    public Property? Property { get; set; }
}

public enum MediaType { Photo, Video }

// ─── Favourite ────────────────────────────────────────────────────────────────
public class Favourite : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid PropertyId { get; set; }

    public User? User { get; set; }
    public Property? Property { get; set; }
}

// ─── Conversation ─────────────────────────────────────────────────────────────
public class Conversation : BaseEntity
{
    public Guid PropertyId { get; set; }
    public Guid BuyerId { get; set; }
    public Guid SellerId { get; set; }
    public DateTime? LastMessageAt { get; set; }

    public Property? Property { get; set; }
    public User? Buyer { get; set; }
    public User? Seller { get; set; }
    public ICollection<Message> Messages { get; set; } = [];
}

// ─── Message ──────────────────────────────────────────────────────────────────
public class Message : BaseEntity
{
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
    public User? Sender { get; set; }
}

// ─── PriceHistory ─────────────────────────────────────────────────────────────
public class PriceHistory : BaseEntity
{
    public Guid PropertyId { get; set; }
    public decimal SuggestedPrice { get; set; }
    public decimal ActualPrice { get; set; }
    public string? AIProvider { get; set; }   // "OpenAI"
    public string? AIReasoning { get; set; }  // AI explanation

    public Property? Property { get; set; }
}
