using Core.Entities;
using Core.DTOs.Auth;
using Core.DTOs.Property;
using Core.DTOs.Search;
using Core.DTOs.Chat;

namespace Core.Interfaces;

// ─── Cache ────────────────────────────────────────────────────────────────────
// Start with in-memory, swap to Redis with zero code changes
public interface ICacheService
{
    Task<T?> GetAsync<T>(string key);
    Task SetAsync<T>(string key, T value, TimeSpan expiry);
    Task RemoveAsync(string key);
    Task RemoveByPrefixAsync(string prefix);
}

// ─── Auth ─────────────────────────────────────────────────────────────────────
public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<string> GenerateJwtTokenAsync(User user);
}

// ─── Property Repository ──────────────────────────────────────────────────────
// CountryCode is always required — enforces country scoping throughout the app
public interface IPropertyRepository
{
    Task<PagedResult<Property>> SearchAsync(PropertySearchFilter filter, PaginationParams pagination);
    Task<Property?> GetByIdAsync(Guid id, string countryCode);
    Task<List<Property>> GetBySellerAsync(Guid sellerId, string countryCode);
    Task<Property> CreateAsync(Property property);
    Task<Property> UpdateAsync(Property property);
    Task<bool> DeleteAsync(Guid id, Guid sellerId, string countryCode);
    Task IncrementViewCountAsync(Guid id);
    Task AddMediaAsync(Guid propertyId, string url, string publicId, string mediaType);
}

// ─── Property Service ─────────────────────────────────────────────────────────
public interface IPropertyService
{
    Task<PagedResult<PropertySummaryDto>> SearchAsync(PropertySearchFilter filter, PaginationParams pagination, Guid? currentUserId);
    Task<PropertyDetailDto?> GetByIdAsync(Guid id, string countryCode, Guid? currentUserId);
    Task<PropertyDetailDto> CreateAsync(CreatePropertyRequest request, Guid sellerId, string countryCode);
    Task<PropertyDetailDto> UpdateAsync(Guid id, UpdatePropertyRequest request, Guid sellerId, string countryCode);
    Task<bool> DeleteAsync(Guid id, Guid sellerId, string countryCode);
    Task AddMediaAsync(Guid propertyId, string url, string publicId, string mediaType);

    // Seller-specific: returns only this seller's listings with pagination
    Task<PagedResult<PropertySummaryDto>> GetSellerListingsAsync(Guid sellerId, string countryCode, PaginationParams pagination);
}

// ─── Favourite Repository ─────────────────────────────────────────────────────
public interface IFavouriteRepository
{
    Task<List<Property>> GetUserFavouritesAsync(Guid userId, string countryCode);
    Task<bool> IsFavouritedAsync(Guid userId, Guid propertyId);
    Task<bool> ToggleAsync(Guid userId, Guid propertyId);
}

// ─── Chat Repository ──────────────────────────────────────────────────────────
public interface IConversationRepository
{
    Task<List<Conversation>> GetUserConversationsAsync(Guid userId);
    Task<Conversation?> GetByIdAsync(Guid conversationId, Guid userId);
    Task<Conversation?> GetByPropertyAndBuyerAsync(Guid propertyId, Guid buyerId);
    Task<Conversation> GetOrCreateAsync(Guid propertyId, Guid buyerId, Guid sellerId);
    Task<List<Message>> GetMessagesAsync(Guid conversationId, Guid userId, PaginationParams pagination);
    Task<Message> AddMessageAsync(Message message);
    Task MarkAsReadAsync(Guid conversationId, Guid userId);
}

// ─── Media Service ────────────────────────────────────────────────────────────
public interface IMediaService
{
    Task<(string Url, string PublicId)> UploadPhotoAsync(Stream stream, string fileName);
    Task<(string Url, string PublicId)> UploadVideoAsync(Stream stream, string fileName);
    Task<bool> DeleteAsync(string publicId);
}

// ─── Location Service ─────────────────────────────────────────────────────────
public interface ILocationService
{
    Task<(decimal Lat, decimal Lng, string? Region, string? City)?> LookupAsync(string postalCode, string countryCode);
}

// ─── AI Price Service ─────────────────────────────────────────────────────────
public interface IAIPriceService
{
    Task<(decimal SuggestedPrice, string Reasoning)> GetPriceSuggestionAsync(
        Property property,
        string countryCode
    );
}
