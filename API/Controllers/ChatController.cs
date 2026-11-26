using Core.DTOs.Chat;
using Core.DTOs.Search;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ChatController : ControllerBase
{
    private readonly IConversationRepository _conversations;
    private readonly IPropertyRepository     _properties;

    public ChatController(
        IConversationRepository conversations,
        IPropertyRepository     properties)
    {
        _conversations = conversations;
        _properties    = properties;
    }

    /// <summary>GET /api/chat/conversations — All conversations for current user</summary>
    [HttpGet("conversations")]
    public async Task<IActionResult> GetConversations()
    {
        var userId    = GetCurrentUserId();
        var convos    = await _conversations.GetUserConversationsAsync(userId);
        var countryCode = GetCurrentUserCountry();

        var dtos = convos.Select(c =>
        {
            var isbuyer     = c.BuyerId == userId;
            var otherUser   = isbuyer ? c.Seller! : c.Buyer!;
            var lastMessage = c.Messages.MaxBy(m => m.SentAt);

            return new ConversationDto
            {
                Id               = c.Id,
                PropertyId       = c.PropertyId,
                PropertyTitle    = c.Property?.Title ?? string.Empty,
                PropertyImageUrl = c.Property?.Media.FirstOrDefault()?.Url,
                OtherUserId      = otherUser.Id,
                OtherUserName    = otherUser.FullName,
                OtherUserImageUrl= otherUser.ProfileImageUrl,
                LastMessage      = lastMessage?.Content,
                LastMessageAt    = lastMessage?.SentAt,
                UnreadCount      = c.Messages.Count(m => m.SenderId != userId && !m.IsRead)
            };
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>GET /api/chat/conversations/{id}/messages — Load message history</summary>
    [HttpGet("conversations/{conversationId:guid}/messages")]
    public async Task<IActionResult> GetMessages(
        Guid conversationId,
        [FromQuery] PaginationParams pagination)
    {
        var userId   = GetCurrentUserId();
        var messages = await _conversations.GetMessagesAsync(conversationId, userId, pagination);

        var dtos = messages.Select(m => new MessageDto
        {
            Id                = m.Id,
            ConversationId    = m.ConversationId,
            SenderId          = m.SenderId,
            SenderName        = m.Sender?.FullName ?? string.Empty,
            Content           = m.Content,
            IsRead            = m.IsRead,
            SentAt            = m.SentAt,
            IsFromCurrentUser = m.SenderId == userId
        }).ToList();

        return Ok(dtos);
    }

    /// <summary>
    /// POST /api/chat/start — Buyer starts a conversation with a seller.
    /// Creates conversation if it doesn't exist, returns conversation ID.
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartConversation([FromBody] StartConversationRequest request)
    {
        var buyerId     = GetCurrentUserId();
        var countryCode = GetCurrentUserCountry();

        var property = await _properties.GetByIdAsync(request.PropertyId, countryCode)
            ?? throw new KeyNotFoundException("Property not found");

        if (property.SellerId == buyerId)
            return BadRequest(new { error = "You cannot message yourself about your own listing" });

        var conversation = await _conversations.GetOrCreateAsync(
            request.PropertyId,
            buyerId,
            property.SellerId
        );

        return Ok(new { conversationId = conversation.Id });
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────
    private Guid GetCurrentUserId()
    {
        var id = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException();
        return Guid.Parse(id);
    }

    private string GetCurrentUserCountry() =>
        User.FindFirstValue("countryCode") ?? "GB";
}

public class StartConversationRequest
{
    public Guid PropertyId { get; set; }
}
