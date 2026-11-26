using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public class FavouriteRepository : IFavouriteRepository
{
    private readonly AppDbContext _db;

    public FavouriteRepository(AppDbContext db) => _db = db;

    public async Task<List<Property>> GetUserFavouritesAsync(Guid userId, string countryCode) =>
        await _db.Favourites
            .Where(f => f.UserId == userId && f.Property!.CountryCode == countryCode)
            .Include(f => f.Property)
                .ThenInclude(p => p!.Media.Where(m => m.IsPrimary))
            .Include(f => f.Property)
                .ThenInclude(p => p!.Seller)
            .OrderByDescending(f => f.CreatedAt)
            .Select(f => f.Property!)
            .ToListAsync();

    public async Task<bool> IsFavouritedAsync(Guid userId, Guid propertyId) =>
        await _db.Favourites.AnyAsync(f => f.UserId == userId && f.PropertyId == propertyId);

    public async Task<bool> ToggleAsync(Guid userId, Guid propertyId)
    {
        var existing = await _db.Favourites
            .FirstOrDefaultAsync(f => f.UserId == userId && f.PropertyId == propertyId);

        if (existing is not null)
        {
            _db.Favourites.Remove(existing);
            await _db.SaveChangesAsync();
            return false; // now un-favourited
        }

        _db.Favourites.Add(new Favourite { UserId = userId, PropertyId = propertyId });
        await _db.SaveChangesAsync();
        return true; // now favourited
    }
}
