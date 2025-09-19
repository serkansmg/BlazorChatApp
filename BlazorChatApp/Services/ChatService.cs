using BlazorChatApp.Models.Chat;
using BlazorChatApp.Models.Identity;
using Microsoft.EntityFrameworkCore;

namespace BlazorChatApp.Services;

public class ChatService
{
    private readonly ApplicationDbContext _context;

    public ChatService(ApplicationDbContext context)
    {
        _context = context;
    }

    // Kullanıcıların chat listesi (arkadaşlar + gruplar)
    public async Task<List<ChatUser>> GetChatUsersForUserAsync(Guid currentUserId)
    {
        var chatUsers = new List<ChatUser>();

        // Arkadaşları getir
        var friendships = await _context.Friendships
            .Where(f => f.User1Id == currentUserId || f.User2Id == currentUserId)
            .ToListAsync();

        foreach (var friendship in friendships)
        {
            var friendId = friendship.User1Id == currentUserId ? friendship.User2Id : friendship.User1Id;
            var friend = await _context.Users.FindAsync(friendId);

            if (friend != null)
            {
                var lastMessage = await GetLastMessageBetweenUsersAsync(currentUserId, friendId);
                var unreadCount = await GetUnreadMessageCountAsync(currentUserId, friendId);

                chatUsers.Add(new ChatUser
                {
                    Id = friend.Id,
                    Name = friend.DisplayName,
                    AvatarUrl = friend.AvatarUrl,
                    UserType = UserType.User,
                    UnreadMessageCount = unreadCount,
                    LastMessage = FormatLastMessageContent(lastMessage),
                    LastMessageTime = lastMessage?.SentAt ?? friendship.CreatedAt
                });
            }
        }

        // Mevcut kod - mesaj partnerleri (arkadaş olmayanlarda mesajlaştığı kişiler)
        var messagePartners = await _context.ChatMessages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .Select(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
            .Where(id => id.HasValue && id.Value != currentUserId)
            .Distinct()
            .ToListAsync();

        // Sadece arkadaş olmayan mesaj partnerlerini ekle
        foreach (var partnerId in messagePartners.Where(p => p.HasValue))
        {
            if (chatUsers.Any(cu => cu.Id == partnerId.Value)) continue; // Zaten arkadaş olarak eklenmişse skip

            var user = await _context.Users.FindAsync(partnerId.Value);
            if (user != null)
            {
                var lastMessage = await GetLastMessageBetweenUsersAsync(currentUserId, partnerId.Value);
                var unreadCount = await GetUnreadMessageCountAsync(currentUserId, partnerId.Value);

                chatUsers.Add(new ChatUser
                {
                    Id = user.Id,
                    Name = user.DisplayName,
                    AvatarUrl = user.AvatarUrl,
                    UserType = UserType.User,
                    UnreadMessageCount = unreadCount,
                    LastMessage = FormatLastMessageContent(lastMessage),
                    LastMessageTime = lastMessage?.SentAt ?? DateTime.MinValue
                });
            }
        }

        // Grup kodları aynı kalacak...
        var userGroups = await _context.GroupMembers
            .Where(gm => gm.UserId == currentUserId)
            .Include(gm => gm.Group)
            .Select(gm => gm.Group)
            .ToListAsync();

        foreach (var group in userGroups)
        {
            var lastMessage = await GetLastGroupMessageAsync(group.Id);
            var unreadCount = await GetUnreadGroupMessageCountAsync(currentUserId, group.Id);

            chatUsers.Add(new ChatUser
            {
                Id = group.Id,
                Name = group.Name,
                AvatarUrl = group.AvatarUrl,
                UserType = UserType.Group,
                UnreadMessageCount = unreadCount,
                LastMessage = FormatLastMessageContent(lastMessage),
                LastMessageTime = lastMessage?.SentAt ?? DateTime.MinValue
            });
        }

        return chatUsers.OrderByDescending(u => u.LastMessageTime).ToList();
    }

    // İki kullanıcı arasındaki mesajlar
    public async Task<List<ChatMessageModel>> GetMessagesBetweenUsersAsync(Guid userId1, Guid userId2)
    {
        return await _context.ChatMessages
            .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                        (m.SenderId == userId2 && m.ReceiverId == userId1))
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    // Grup mesajları
    public async Task<List<ChatMessageModel>> GetGroupMessagesAsync(Guid groupId)
    {
        return await _context.ChatMessages
            .Where(m => m.GroupId == groupId)
            .Include(m => m.Sender)
            .OrderBy(m => m.SentAt)
            .ToListAsync();
    }

    // Mesaj gönder
    public async Task<ChatMessageModel> SendMessageAsync(ChatMessageModel message)
    {
        _context.ChatMessages.Add(message);
        await _context.SaveChangesAsync();
        return message;
    }

    // Mesajları okundu olarak işaretle
    public async Task MarkMessagesAsReadAsync(Guid currentUserId, Guid otherUserId)
    {
        var unreadMessages = await _context.ChatMessages
            .Where(m => m.SenderId == otherUserId && m.ReceiverId == currentUserId && !m.IsRead)
            .ToListAsync();

        foreach (var message in unreadMessages)
        {
            message.IsRead = true;
        }

        await _context.SaveChangesAsync();
    }

    // Helper methods
    private async Task<ChatMessageModel?> GetLastMessageBetweenUsersAsync(Guid userId1, Guid userId2)
    {
        return await _context.ChatMessages
            .Where(m => (m.SenderId == userId1 && m.ReceiverId == userId2) ||
                        (m.SenderId == userId2 && m.ReceiverId == userId1))
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
    }

    private async Task<int> GetUnreadMessageCountAsync(Guid currentUserId, Guid otherUserId)
    {
        return await _context.ChatMessages
            .CountAsync(m => m.SenderId == otherUserId && m.ReceiverId == currentUserId && !m.IsRead);
    }

    private async Task<ChatMessageModel?> GetLastGroupMessageAsync(Guid groupId)
    {
        return await _context.ChatMessages
            .Where(m => m.GroupId == groupId)
            .OrderByDescending(m => m.SentAt)
            .FirstOrDefaultAsync();
    }
    private string FormatLastMessageContent(ChatMessageModel? message)
    {
        if (message == null) return "Mesaj yok";
    
        return message.MessageType switch
        {
            MessageType.Text => message.Content,
            MessageType.Image => "📷 Resim",
            MessageType.Video => "🎥 Video", 
            MessageType.Audio => "🎵 Ses",
            MessageType.File => $"📎 {message.FileName}",
            _ => message.Content
        };
    }

    private async Task<int> GetUnreadGroupMessageCountAsync(Guid currentUserId, Guid groupId)
    {
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.UserId == currentUserId && gm.GroupId == groupId);
        
        if (membership == null) return 0;
    
        var lastReadTime = membership.LastReadAt ?? membership.JoinedAt;
    
        return await _context.ChatMessages
            .CountAsync(m => m.GroupId == groupId && 
                             m.SenderId != currentUserId && 
                             m.SentAt > lastReadTime);
    }

    public async Task MarkGroupMessagesAsReadAsync(Guid currentUserId, Guid groupId)
    {
        var membership = await _context.GroupMembers
            .FirstOrDefaultAsync(gm => gm.UserId == currentUserId && gm.GroupId == groupId);
        
        if (membership != null)
        {
            membership.LastReadAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }
    }
    
    
     
}