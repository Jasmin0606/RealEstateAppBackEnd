using Core.DTOs.Property;
using Core.DTOs.Search;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PropertiesController : ControllerBase
{
    private readonly IPropertyService _properties;
    private readonly IMediaService _media;
    private readonly IAIPriceService _aiPrice;
    private readonly IFavouriteRepository _favourites;

    public PropertiesController(
        IPropertyService properties,
        IMediaService media,
        IAIPriceService aiPrice,
        IFavouriteRepository favourites)
    {
        _properties = properties;
        _media = media;
        _aiPrice = aiPrice;
        _favourites = favourites;
    }

    // ── GET /api/properties ───────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] PropertySearchFilter filter,
        [FromQuery] PaginationParams pagination)
    {
        if (string.IsNullOrEmpty(filter.CountryCode))
            filter.CountryCode = "GB";

        var currentUserId = GetCurrentUserId();
        var result = await _properties.SearchAsync(filter, pagination, currentUserId);
        return Ok(result);
    }

    // ── GET /api/properties/{id} ──────────────────────────────────────────────
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] string countryCode = "GB")
    {
        var currentUserId = GetCurrentUserId();
        var property = await _properties.GetByIdAsync(id, countryCode, currentUserId);
        return property is null ? NotFound() : Ok(property);
    }

    // ── POST /api/properties ──────────────────────────────────────────────────
    [HttpPost, Authorize]
    public async Task<IActionResult> Create([FromBody] CreatePropertyRequest request)
    {
        var sellerId = GetCurrentUserIdRequired();
        var countryCode = GetCurrentUserCountry();
        var property = await _properties.CreateAsync(request, sellerId, countryCode);
        return CreatedAtAction(nameof(GetById), new { id = property.Id, countryCode }, property);
    }

    // ── PUT /api/properties/{id} ──────────────────────────────────────────────
    [HttpPut("{id:guid}"), Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePropertyRequest request)
    {
        var sellerId = GetCurrentUserIdRequired();
        var countryCode = GetCurrentUserCountry();
        var property = await _properties.UpdateAsync(id, request, sellerId, countryCode);
        return Ok(property);
    }

    // ── DELETE /api/properties/{id} ───────────────────────────────────────────
    [HttpDelete("{id:guid}"), Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        var sellerId = GetCurrentUserIdRequired();
        var countryCode = GetCurrentUserCountry();
        var deleted = await _properties.DeleteAsync(id, sellerId, countryCode);
        return deleted ? NoContent() : NotFound();
    }

    // ── POST /api/properties/{id}/media ───────────────────────────────────────
    [HttpPost("{id:guid}/media"), Authorize]
    public async Task<IActionResult> UploadMedia(Guid id, IFormFile file)
    {
        if (file == null || file.Length == 0)
            return BadRequest("No file provided");

        var contentType = file.ContentType.ToLowerInvariant();
        var allowedPhotoTypes = new[] { "image/jpeg", "image/jpg", "image/png", "image/webp", "image/gif" };
        var allowedVideoTypes = new[] { "video/mp4", "video/mov", "video/quicktime", "video/mpeg" };
        var isPhoto = allowedPhotoTypes.Contains(contentType);
        var isVideo = allowedVideoTypes.Contains(contentType);

        if (!isPhoto && !isVideo)
            return BadRequest($"File type '{contentType}' not allowed.");

        if (isPhoto && file.Length > 10 * 1024 * 1024)
            return BadRequest("Photo must be under 10MB");

        if (isVideo && file.Length > 200 * 1024 * 1024)
            return BadRequest("Video must be under 200MB");

        await using var stream = file.OpenReadStream();

        var (url, publicId) = isPhoto
            ? await _media.UploadPhotoAsync(stream, file.FileName)
            : await _media.UploadVideoAsync(stream, file.FileName);

        // ← This was missing — save to DB
        var mediaType = isPhoto ? "Photo" : "Video";
        await _properties.AddMediaAsync(id, url, publicId, mediaType);

        return Ok(new { url, publicId, mediaType });
    }

    // ── POST /api/properties/{id}/favourite ───────────────────────────────────
    [HttpPost("{id:guid}/favourite"), Authorize]
    public async Task<IActionResult> ToggleFavourite(Guid id)
    {
        var userId = GetCurrentUserIdRequired();
        var isFavNow = await _favourites.ToggleAsync(userId, id);
        return Ok(new { isFavourited = isFavNow });
    }

    // ── GET /api/properties/favourites ────────────────────────────────────────
    [HttpGet("favourites"), Authorize]
    public async Task<IActionResult> GetFavourites()
    {
        var userId = GetCurrentUserIdRequired();
        var countryCode = GetCurrentUserCountry();

        // Get favourite properties and map to summary DTOs
        var properties = await _favourites.GetUserFavouritesAsync(userId, countryCode);
        var config = Shared.Constants.SupportedCountries.GetByCode(countryCode);

        var dtos = properties.Select(p => new PropertySummaryDto
        {
            Id = p.Id,
            Title = p.Title,
            Price = p.Price,
            CurrencySymbol = config?.CurrencySymbol ?? "£",
            PropertyType = p.PropertyType,
            ListingType = p.ListingType.ToString(),
            Status = p.Status.ToString(),
            Bedrooms = p.Bedrooms,
            Bathrooms = p.Bathrooms,
            SizeValue = p.SizeValue,
            SizeUnit = p.SizeUnit,
            Region = p.Region,
            PostalCode = p.PostalCode,
            City = p.City,
            PrimaryImageUrl = p.Media.FirstOrDefault(m => m.IsPrimary)?.Url
                           ?? p.Media.FirstOrDefault()?.Url,
            FavouriteCount = p.Favourites.Count,
            ViewCount = p.ViewCount,
            IsFeatured = p.IsFeatured,
            CreatedAt = p.CreatedAt,
            Seller = p.Seller is null ? null : new SellerSummaryDto
            {
                Id = p.Seller.Id,
                FullName = p.Seller.FullName,
                ProfileImageUrl = p.Seller.ProfileImageUrl,
                IsVerified = p.Seller.IsVerified
            }
        }).ToList();

        return Ok(dtos);
    }

    // ── GET /api/properties/my-listings ──────────────────────────────────────
    [HttpGet("my-listings"), Authorize]
    public async Task<IActionResult> GetMyListings(
        [FromQuery] PaginationParams pagination)
    {
        var sellerId = GetCurrentUserIdRequired();
        var countryCode = GetCurrentUserCountry();

        var filter = new PropertySearchFilter
        {
            CountryCode = countryCode,
            SortBy = "CreatedAt",
            SortDirection = "desc"
        };

        // Override search to only return this seller's properties
        var result = await _properties.GetSellerListingsAsync(sellerId, countryCode, pagination);
        return Ok(result);
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private Guid? GetCurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(id, out var guid) ? guid : null;
    }

    private Guid GetCurrentUserIdRequired() =>
        GetCurrentUserId() ?? throw new UnauthorizedAccessException();

    private string GetCurrentUserCountry() =>
        User.FindFirstValue("countryCode") ?? "GB";
}
