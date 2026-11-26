using Core.DTOs.Chat;
using Core.Entities;
using Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace API.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly IConversationRepository _conversations;
    private readonly ILogger<ChatHub> _logger;

    public ChatHub(IConversationRepository conversations, ILogger<ChatHub> logger)
    {
        _conversations = conversations;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("[ChatHub] Connected: {ConnId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("[ChatHub] Disconnected: {ConnId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversation(string conversationId)
    {
        _logger.LogInformation("[ChatHub] JoinConversation: {ConvId}", conversationId);
        await Groups.AddToGroupAsync(Context.ConnectionId, conversationId);
    }

    public async Task LeaveConversation(string conversationId)
        => await Groups.RemoveFromGroupAsync(Context.ConnectionId, conversationId);

    public async Task SendMessage(string conversationId, string content)
    {
        var senderId = GetCurrentUserId();
        var convId = Guid.Parse(conversationId);

        var message = new Message
        {
            ConversationId = convId,
            SenderId = senderId,
            Content = content,
            SentAt = DateTime.UtcNow
        };

        var saved = await _conversations.AddMessageAsync(message);

        var dto = new MessageDto
        {
            Id = saved.Id,
            ConversationId = saved.ConversationId,
            SenderId = saved.SenderId,
            SenderName = Context.User?.FindFirstValue(ClaimTypes.Name) ?? string.Empty,
            Content = saved.Content,
            IsRead = false,
            SentAt = saved.SentAt,
            IsFromCurrentUser = true
        };

        await Clients.Group(conversationId).SendAsync("ReceiveMessage", dto);
    }

    public async Task MarkAsRead(string conversationId)
    {
        var userId = GetCurrentUserId();
        await _conversations.MarkAsReadAsync(Guid.Parse(conversationId), userId);
        await Clients.Group(conversationId).SendAsync("MessagesRead", conversationId, userId);
    }

    private Guid GetCurrentUserId()
    {
        // Check both short and full URI claim names
        var id = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
              ?? Context.User?.FindFirstValue(
                 "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")
              ?? throw new HubException("Not authenticated");
        return Guid.Parse(id);
    }
}
