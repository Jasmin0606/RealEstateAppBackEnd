namespace Core.DTOs.Search;

// ─── Pagination ───────────────────────────────────────────────────────────────
public class PaginationParams
{
    private const int MaxPageSize = 50;
    private int _pageSize = 20;

    public int Page { get; set; } = 1;
    public int PageSize
    {
        get => _pageSize;
        set => _pageSize = value > MaxPageSize ? MaxPageSize : value;
    }
}

public class PagedResult<T>
{
    public List<T> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasNextPage => Page < TotalPages;
    public bool HasPreviousPage => Page > 1;
}

// ─── Property Search Filter ───────────────────────────────────────────────────
public class PropertySearchFilter
{
    // Always required — country scoping
    public string CountryCode { get; set; } = string.Empty;

    // Text search
    public string? Keyword { get; set; }

    // Location
    public string? Region { get; set; }       // Borough / State / Emirate
    public string? PostalCode { get; set; }
    public string? City { get; set; }

    // Property details
    public string? PropertyType { get; set; }
    public string? ListingType { get; set; }  // Sale | Rent

    // Price range
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }

    // Size
    public int? MinBedrooms { get; set; }
    public int? MaxBedrooms { get; set; }
    public int? MinBathrooms { get; set; }

    // Sort
    public string SortBy { get; set; } = "CreatedAt";     // Price, CreatedAt, Bedrooms
    public string SortDirection { get; set; } = "desc";   // asc | desc
}
