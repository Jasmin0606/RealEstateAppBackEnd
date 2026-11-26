using System.ComponentModel.DataAnnotations;

namespace Core.DTOs.Chat;

public class SendMessageRequest
{
    [Required]
    public Guid PropertyId { get; set; }

    [Required, MinLength(1), MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}

public class ConversationDto
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string PropertyTitle { get; set; } = string.Empty;
    public string? PropertyImageUrl { get; set; }
    public Guid OtherUserId { get; set; }
    public string OtherUserName { get; set; } = string.Empty;
    public string? OtherUserImageUrl { get; set; }
    public string? LastMessage { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public int UnreadCount { get; set; }
}

public class MessageDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid SenderId { get; set; }
    public string SenderName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime SentAt { get; set; }
    public bool IsFromCurrentUser { get; set; }
}
