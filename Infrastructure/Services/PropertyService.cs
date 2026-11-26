using Core.DTOs.Property;
using Core.DTOs.Search;
using Core.Entities;
using Core.Interfaces;
using Shared.Constants;

namespace Infrastructure.Services;

public class PropertyService : IPropertyService
{
    private readonly IPropertyRepository _repo;
    private readonly ILocationService    _location;
    private readonly IAIPriceService     _aiPrice;
    private readonly ICacheService       _cache;

    public PropertyService(
        IPropertyRepository repo,
        ILocationService    location,
        IAIPriceService     aiPrice,
        ICacheService       cache)
    {
        _repo     = repo;
        _location = location;
        _aiPrice  = aiPrice;
        _cache    = cache;
    }

    public async Task<PagedResult<PropertySummaryDto>> SearchAsync(
        PropertySearchFilter filter,
        PaginationParams     pagination,
        Guid?                currentUserId)
    {
        var result = await _repo.SearchAsync(filter, pagination);
        var items  = result.Items.Select(p => MapToSummary(p, filter.CountryCode)).ToList();

        return new PagedResult<PropertySummaryDto>
        {
            Items      = items,
            TotalCount = result.TotalCount,
            Page       = result.Page,
            PageSize   = result.PageSize
        };
    }

    public async Task<PropertyDetailDto?> GetByIdAsync(Guid id, string countryCode, Guid? currentUserId)
    {
        var cacheKey = CacheKeys.PropertyDetail(countryCode, id);
        var cached   = await _cache.GetAsync<PropertyDetailDto>(cacheKey);
        if (cached is not null)
        {
            if (currentUserId.HasValue)
                cached.IsFavourited = cached.Seller?.Id != currentUserId;
            return cached;
        }

        var property = await _repo.GetByIdAsync(id, countryCode);
        if (property is null) return null;

        await _repo.IncrementViewCountAsync(id);

        var dto = MapToDetail(property, countryCode);
        dto.IsFavourited = currentUserId.HasValue &&
                           property.Favourites.Any(f => f.UserId == currentUserId);

        await _cache.SetAsync(cacheKey, dto, TimeSpan.FromMinutes(10));
        return dto;
    }

    public async Task<PropertyDetailDto> CreateAsync(
        CreatePropertyRequest request,
        Guid                  sellerId,
        string                countryCode)
    {
        // Resolve lat/lng from postcode
        decimal? lat = null, lng = null;
        string?  region = request.Region, city = request.City;

        var location = await _location.LookupAsync(request.PostalCode, countryCode);
        if (location.HasValue)
        {
            lat    = location.Value.Lat;
            lng    = location.Value.Lng;
            region = location.Value.Region ?? request.Region;
            city   = location.Value.City   ?? request.City;
        }

        var config       = SupportedCountries.GetByCode(countryCode);
        var currencyCode = config?.CurrencyCode ?? "GBP";

        var property = new Property
        {
            SellerId          = sellerId,
            CountryCode       = countryCode,
            Title             = request.Title.Trim(),
            Description       = request.Description?.Trim(),
            Price             = request.Price,
            CurrencyCode      = currencyCode,
            PropertyType      = request.PropertyType,
            ListingType       = Enum.TryParse<ListingType>(request.ListingType, ignoreCase: true, out var lt) ? lt : ListingType.Sale,
            Bedrooms          = request.Bedrooms,
            Bathrooms         = request.Bathrooms,
            SizeValue         = request.SizeValue,
            SizeUnit          = request.SizeUnit ?? config?.SizeUnit,
            AddressLine1      = request.AddressLine1.Trim(),
            AddressLine2      = request.AddressLine2?.Trim(),
            City              = city,
            Region            = region,
            PostalCode        = request.PostalCode.Trim().ToUpper(),
            Latitude          = lat,
            Longitude         = lng,
            CountryAttributes = request.CountryAttributes,
            Status            = PropertyStatus.Active
        };

        // AI price suggestion — non-fatal if it fails
        try
        {
            var (suggested, _) = await _aiPrice.GetPriceSuggestionAsync(property, countryCode);
            property.SuggestedPrice = suggested;
            property.SuggestedAt    = DateTime.UtcNow;
        }
        catch { /* AI failure must never block listing creation */ }

        var created = await _repo.CreateAsync(property);
        await _cache.RemoveAsync(CacheKeys.SellerProperties(countryCode, sellerId));

        return MapToDetail(created, countryCode);
    }

    public async Task<PropertyDetailDto> UpdateAsync(
        Guid                  id,
        UpdatePropertyRequest request,
        Guid                  sellerId,
        string                countryCode)
    {
        var property = await _repo.GetByIdAsync(id, countryCode)
            ?? throw new KeyNotFoundException("Property not found");

        if (property.SellerId != sellerId)
            throw new UnauthorizedAccessException("You can only edit your own listings");

        property.Title             = request.Title.Trim();
        property.Description       = request.Description?.Trim();
        property.Price             = request.Price;
        property.PropertyType      = request.PropertyType;
        property.Bedrooms          = request.Bedrooms;
        property.Bathrooms         = request.Bathrooms;
        property.SizeValue         = request.SizeValue;
        property.SizeUnit          = request.SizeUnit;
        property.AddressLine1      = request.AddressLine1.Trim();
        property.AddressLine2      = request.AddressLine2?.Trim();
        property.Region            = request.Region;
        property.PostalCode        = request.PostalCode.Trim().ToUpper();
        property.CountryAttributes = request.CountryAttributes;

        if (!string.IsNullOrEmpty(request.Status) &&
            Enum.TryParse<PropertyStatus>(request.Status, out var status))
            property.Status = status;

        var updated = await _repo.UpdateAsync(property);

        await _cache.RemoveAsync(CacheKeys.PropertyDetail(countryCode, id));
        await _cache.RemoveAsync(CacheKeys.SellerProperties(countryCode, sellerId));

        return MapToDetail(updated, countryCode);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid sellerId, string countryCode)
    {
        var deleted = await _repo.DeleteAsync(id, sellerId, countryCode);
        if (deleted)
        {
            await _cache.RemoveAsync(CacheKeys.PropertyDetail(countryCode, id));
            await _cache.RemoveAsync(CacheKeys.SellerProperties(countryCode, sellerId));
        }
        return deleted;
    }


    public async Task AddMediaAsync(Guid propertyId, string url, string publicId, string mediaType)
    {
        await _repo.AddMediaAsync(propertyId, url, publicId, mediaType);
        // Invalidate cache so detail page shows new photo
        await _cache.RemoveAsync(CacheKeys.PropertyDetail("GB", propertyId));
    }

    public async Task<PagedResult<PropertySummaryDto>> GetSellerListingsAsync(
    Guid sellerId,
    string countryCode,
    PaginationParams pagination)
    {
        var properties = await _repo.GetBySellerAsync(sellerId, countryCode);

        var totalCount = properties.Count;
        var items = properties
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .Select(p => MapToSummary(p, countryCode))
            .ToList();

        return new PagedResult<PropertySummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    // ─── Mappers ──────────────────────────────────────────────────────────────
    private static PropertySummaryDto MapToSummary(Property p, string countryCode)
    {
        var config = SupportedCountries.GetByCode(countryCode);
        return new PropertySummaryDto
        {
            Id              = p.Id,
            Title           = p.Title,
            Price           = p.Price,
            CurrencySymbol  = config?.CurrencySymbol ?? "£",
            PropertyType    = p.PropertyType,
            ListingType     = p.ListingType.ToString(),
            Status          = p.Status.ToString(),
            Bedrooms        = p.Bedrooms,
            Bathrooms       = p.Bathrooms,
            SizeValue       = p.SizeValue,
            SizeUnit        = p.SizeUnit,
            Region          = p.Region,
            PostalCode      = p.PostalCode,
            City            = p.City,
            PrimaryImageUrl = p.Media.FirstOrDefault(m => m.IsPrimary)?.Url
                           ?? p.Media.FirstOrDefault()?.Url,
            FavouriteCount  = p.Favourites.Count,
            ViewCount       = p.ViewCount,
            IsFeatured      = p.IsFeatured,
            CreatedAt       = p.CreatedAt,
            Seller          = p.Seller is null ? null : new SellerSummaryDto
            {
                Id              = p.Seller.Id,
                FullName        = p.Seller.FullName,
                ProfileImageUrl = p.Seller.ProfileImageUrl,
                IsVerified      = p.Seller.IsVerified
            }
        };
    }

    private static PropertyDetailDto MapToDetail(Property p, string countryCode)
    {
        var summary = MapToSummary(p, countryCode);
        return new PropertyDetailDto
        {
            Id              = summary.Id,
            Title           = summary.Title,
            Price           = summary.Price,
            CurrencySymbol  = summary.CurrencySymbol,
            PropertyType    = summary.PropertyType,
            ListingType     = summary.ListingType,
            Status          = summary.Status,
            Bedrooms        = summary.Bedrooms,
            Bathrooms       = summary.Bathrooms,
            SizeValue       = summary.SizeValue,
            SizeUnit        = summary.SizeUnit,
            Region          = summary.Region,
            PostalCode      = summary.PostalCode,
            City            = summary.City,
            PrimaryImageUrl = summary.PrimaryImageUrl,
            FavouriteCount  = summary.FavouriteCount,
            ViewCount       = summary.ViewCount,
            IsFeatured      = summary.IsFeatured,
            CreatedAt       = summary.CreatedAt,
            Seller          = summary.Seller,
            Description       = p.Description,
            AddressLine1      = p.AddressLine1,
            AddressLine2      = p.AddressLine2,
            Latitude          = p.Latitude,
            Longitude         = p.Longitude,
            CountryAttributes = p.CountryAttributes,
            SuggestedPrice    = p.SuggestedPrice,
            Media             = p.Media
                .OrderBy(m => m.DisplayOrder)
                .Select(m => new PropertyMediaDto
                {
                    Id           = m.Id,
                    MediaType    = m.MediaType.ToString(),
                    Url          = m.Url,
                    IsPrimary    = m.IsPrimary,
                    DisplayOrder = m.DisplayOrder
                }).ToList()
        };
    }


}
