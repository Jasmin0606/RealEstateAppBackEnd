using Core.DTOs.Search;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public class PropertyRepository : IPropertyRepository
{
    private readonly AppDbContext _db;

    public PropertyRepository(AppDbContext db) => _db = db;

    public async Task<PagedResult<Property>> SearchAsync(
        PropertySearchFilter filter,
        PaginationParams pagination)
    {
        // Always scope by country — non-negotiable
        var query = _db.Properties
            .Where(p => p.CountryCode == filter.CountryCode
                     && p.Status == PropertyStatus.Active)
            .Include(p => p.Media.Where(m => m.IsPrimary))
            .Include(p => p.Seller)
            .AsQueryable();

        // ── Filters ──────────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.ToLower();
            query = query.Where(p =>
                p.Title.ToLower().Contains(kw) ||
                (p.Description != null && p.Description.ToLower().Contains(kw)) ||
                (p.City != null && p.City.ToLower().Contains(kw)));
        }

        if (!string.IsNullOrWhiteSpace(filter.Region))
            query = query.Where(p => p.Region == filter.Region);

        if (!string.IsNullOrWhiteSpace(filter.PostalCode))
            query = query.Where(p => p.PostalCode != null &&
                                     p.PostalCode.StartsWith(filter.PostalCode));

        if (!string.IsNullOrWhiteSpace(filter.PropertyType))
            query = query.Where(p => p.PropertyType == filter.PropertyType);

        if (!string.IsNullOrWhiteSpace(filter.ListingType) &&
            Enum.TryParse<ListingType>(filter.ListingType, out var listingType))
            query = query.Where(p => p.ListingType == listingType);

        if (filter.MinPrice.HasValue)
            query = query.Where(p => p.Price >= filter.MinPrice.Value);

        if (filter.MaxPrice.HasValue)
            query = query.Where(p => p.Price <= filter.MaxPrice.Value);

        if (filter.MinBedrooms.HasValue)
            query = query.Where(p => p.Bedrooms >= filter.MinBedrooms.Value);

        if (filter.MaxBedrooms.HasValue)
            query = query.Where(p => p.Bedrooms <= filter.MaxBedrooms.Value);

        if (filter.MinBathrooms.HasValue)
            query = query.Where(p => p.Bathrooms >= filter.MinBathrooms.Value);

        // ── Sorting ───────────────────────────────────────────────────────────
        query = filter.SortBy.ToLower() switch
        {
            "price" => filter.SortDirection == "asc" ? query.OrderBy(p => p.Price) : query.OrderByDescending(p => p.Price),
            "bedrooms" => filter.SortDirection == "asc" ? query.OrderBy(p => p.Bedrooms) : query.OrderByDescending(p => p.Bedrooms),
            _ => query.OrderByDescending(p => p.CreatedAt) // Default: newest first
        };

        // ── Pagination ────────────────────────────────────────────────────────
        var totalCount = await query.CountAsync();

        var items = await query
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .ToListAsync();

        return new PagedResult<Property>
        {
            Items = items,
            TotalCount = totalCount,
            Page = pagination.Page,
            PageSize = pagination.PageSize
        };
    }

    public async Task<Property?> GetByIdAsync(Guid id, string countryCode) =>
        await _db.Properties
            .Where(p => p.Id == id && p.CountryCode == countryCode)
            .Include(p => p.Media.OrderBy(m => m.DisplayOrder))
            .Include(p => p.Seller)
            .Include(p => p.Favourites)
            .FirstOrDefaultAsync();

    public async Task<List<Property>> GetBySellerAsync(Guid sellerId, string countryCode) =>
        await _db.Properties
            .Where(p => p.SellerId == sellerId && p.CountryCode == countryCode)
            .Include(p => p.Media.Where(m => m.IsPrimary))
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

    public async Task<Property> CreateAsync(Property property)
    {
        _db.Properties.Add(property);
        await _db.SaveChangesAsync();
        return property;
    }

    public async Task<Property> UpdateAsync(Property property)
    {
        _db.Properties.Update(property);
        await _db.SaveChangesAsync();
        return property;
    }

    public async Task<bool> DeleteAsync(Guid id, Guid sellerId, string countryCode)
    {
        var property = await _db.Properties
            .FirstOrDefaultAsync(p => p.Id == id
                                   && p.SellerId == sellerId
                                   && p.CountryCode == countryCode);
        if (property is null) return false;

        _db.Properties.Remove(property);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task IncrementViewCountAsync(Guid id) =>
        await _db.Properties
            .Where(p => p.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.ViewCount, p => p.ViewCount + 1));


    public async Task AddMediaAsync(Guid propertyId, string url, string publicId, string mediaType)
    {
        var existingCount = await _db.PropertyMedia
            .CountAsync(m => m.PropertyId == propertyId);

        var media = new PropertyMedia
        {
            PropertyId = propertyId,
            Url = url,
            PublicId = publicId,
            MediaType = Enum.Parse<MediaType>(mediaType),
            IsPrimary = existingCount == 0,
            DisplayOrder = existingCount
        };

        _db.PropertyMedia.Add(media);
        await _db.SaveChangesAsync();
    }
}
