using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Property;

// ─── Request DTOs ─────────────────────────────────────────────────────────────
public class CreatePropertyRequest
{
    [Required, MinLength(10), MaxLength(300)]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required, Range(1, double.MaxValue)]
    public decimal Price { get; set; }

    [Required]
    public string PropertyType { get; set; } = string.Empty;

    [Required]
    public string ListingType { get; set; } = "Sale";   // Sale | Rent

    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }

    // Location
    [Required]
    public string AddressLine1 { get; set; } = string.Empty;
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }

    [Required]
    public string Region { get; set; } = string.Empty;   // Borough for UK

    [Required]
    public string PostalCode { get; set; } = string.Empty;

    // Country-specific JSON fields
    // UK example: { "Tenure": "Leasehold", "EPCRating": "C", "CouncilTaxBand": "D" }
    public string? CountryAttributes { get; set; }
}

public class UpdatePropertyRequest : CreatePropertyRequest
{
    public string? Status { get; set; }
}

// ─── Response DTOs ────────────────────────────────────────────────────────────
public class PropertySummaryDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string CurrencySymbol { get; set; } = string.Empty;
    public string PropertyType { get; set; } = string.Empty;
    public string ListingType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int? Bedrooms { get; set; }
    public int? Bathrooms { get; set; }
    public decimal? SizeValue { get; set; }
    public string? SizeUnit { get; set; }
    public string? Region { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public int FavouriteCount { get; set; }
    public int ViewCount { get; set; }
    public bool IsFeatured { get; set; }
    public DateTime CreatedAt { get; set; }
    public SellerSummaryDto? Seller { get; set; }
}

public class PropertyDetailDto : PropertySummaryDto
{
    public string? Description { get; set; }
    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? CountryAttributes { get; set; }  // JSON
    public decimal? SuggestedPrice { get; set; }
    public List<PropertyMediaDto> Media { get; set; } = [];
    public bool IsFavourited { get; set; }           // For current user
}

public class PropertyMediaDto
{
    public Guid Id { get; set; }
    public string MediaType { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public int DisplayOrder { get; set; }
}

public class SellerSummaryDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? ProfileImageUrl { get; set; }
    public bool IsVerified { get; set; }
}
