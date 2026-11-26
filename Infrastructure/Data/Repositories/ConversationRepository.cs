using Core.DTOs.Search;
using Core.Entities;
using Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Data.Repositories;

public class ConversationRepository : IConversationRepository
{
    private readonly AppDbContext _db;

    public ConversationRepository(AppDbContext db) => _db = db;

    public async Task<List<Conversation>> GetUserConversationsAsync(Guid userId) =>
        await _db.Conversations
            .Where(c => c.BuyerId == userId || c.SellerId == userId)
            .Include(c => c.Property)
                .ThenInclude(p => p!.Media.Where(m => m.IsPrimary))
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .Include(c => c.Messages.OrderByDescending(m => m.SentAt).Take(1))
            .OrderByDescending(c => c.LastMessageAt ?? c.CreatedAt)
            .ToListAsync();

    public async Task<Conversation?> GetByIdAsync(Guid conversationId, Guid userId) =>
        await _db.Conversations
            .Where(c => c.Id == conversationId && (c.BuyerId == userId || c.SellerId == userId))
            .Include(c => c.Property)
            .Include(c => c.Buyer)
            .Include(c => c.Seller)
            .FirstOrDefaultAsync();

    public async Task<Conversation?> GetByPropertyAndBuyerAsync(Guid propertyId, Guid buyerId) =>
        await _db.Conversations
            .FirstOrDefaultAsync(c => c.PropertyId == propertyId && c.BuyerId == buyerId);

    public async Task<Conversation> GetOrCreateAsync(Guid propertyId, Guid buyerId, Guid sellerId)
    {
        var existing = await GetByPropertyAndBuyerAsync(propertyId, buyerId);
        if (existing is not null) return existing;

        var conversation = new Conversation
        {
            PropertyId = propertyId,
            BuyerId    = buyerId,
            SellerId   = sellerId
        };

        _db.Conversations.Add(conversation);
        await _db.SaveChangesAsync();
        return conversation;
    }

    public async Task<List<Message>> GetMessagesAsync(
        Guid             conversationId,
        Guid             userId,
        PaginationParams pagination)
    {
        // Verify user belongs to this conversation
        var isMember = await _db.Conversations.AnyAsync(c =>
            c.Id == conversationId && (c.BuyerId == userId || c.SellerId == userId));

        if (!isMember) throw new UnauthorizedAccessException("Not a member of this conversation");

        return await _db.Messages
            .Where(m => m.ConversationId == conversationId)
            .Include(m => m.Sender)
            .OrderByDescending(m => m.SentAt)
            .Skip((pagination.Page - 1) * pagination.PageSize)
            .Take(pagination.PageSize)
            .OrderBy(m => m.SentAt)  // Re-order ascending for display
            .ToListAsync();
    }

    public async Task<Message> AddMessageAsync(Message message)
    {
        _db.Messages.Add(message);

        // Update LastMessageAt on conversation
        var conversation = await _db.Conversations.FindAsync(message.ConversationId);
        if (conversation is not null)
            conversation.LastMessageAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return message;
    }

    public async Task MarkAsReadAsync(Guid conversationId, Guid userId)
    {
        await _db.Messages
            .Where(m => m.ConversationId == conversationId
                     && m.SenderId != userId
                     && !m.IsRead)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.IsRead, true));
    }
}
