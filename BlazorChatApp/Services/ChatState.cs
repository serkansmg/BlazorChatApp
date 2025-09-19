using BlazorChatApp.Models.Chat;
using System.Collections.Concurrent;

namespace BlazorChatApp.Services;

public class ChatState
{
    private readonly ConcurrentDictionary<string, List<ChatMessageModel>> _messagesByUser = new();
    private readonly ConcurrentDictionary<Guid, ChatUser> _chatUsers = new();
    private readonly EventBus _eventBus;
    private readonly ChatService _chatService;
    
    public ChatState(EventBus eventBus, ChatService chatService)
    {
        _eventBus = eventBus;
        _chatService = chatService;
    }
    
    // Database'den chat kullanıcılarını yükle
    public async Task LoadChatUsersFromDatabaseAsync(Guid currentUserId)
    {
        var users = await _chatService.GetChatUsersForUserAsync(currentUserId);
        
        _chatUsers.Clear();
        foreach (var user in users)
        {
            _chatUsers[user.Id] = user;
        }
    }
    
    public List<ChatUser> GetChatUsers()
    {
        return _chatUsers.Values.OrderByDescending(u => u.LastMessageTime).ToList();
    }
    
    public void AddOrUpdateUser(ChatUser user)
    {
        _chatUsers.AddOrUpdate(user.Id, user, (key, existing) => user);
        _eventBus.PublishUserStatusChange(user);
    }
    
    // Database'den mesajları yükle ve cache'le
    public async Task<List<ChatMessageModel>> GetMessagesForUserAsync(Guid currentUserId, Guid otherUserId)
    {
        var key = GetConversationKey(currentUserId, otherUserId);
        
        // Cache'de var mı kontrol et
        if (_messagesByUser.TryGetValue(key, out var cachedMessages))
        {
            return cachedMessages.OrderBy(m => m.SentAt).ToList();
        }
        
        // Database'den yükle
        var messages = await _chatService.GetMessagesBetweenUsersAsync(currentUserId, otherUserId);
        _messagesByUser[key] = messages;
        
        return messages.OrderBy(m => m.SentAt).ToList();
    }
    
    // Database'den grup mesajları yükle ve cache'le
    public async Task<List<ChatMessageModel>> GetMessagesForGroupAsync(Guid groupId)
    {
        var key = $"group-{groupId}";
        
        // Cache'de var mı kontrol et
        if (_messagesByUser.TryGetValue(key, out var cachedMessages))
        {
            return cachedMessages.OrderBy(m => m.SentAt).ToList();
        }
        
        // Database'den yükle
        var messages = await _chatService.GetGroupMessagesAsync(groupId);
        _messagesByUser[key] = messages;
        
        return messages.OrderBy(m => m.SentAt).ToList();
    }
    
    // Mesaj gönder (database'e kaydet ve cache'i güncelle)
    public async Task AddMessageAsync(ChatMessageModel message)
    {
        // Database'e kaydet
        var savedMessage = await _chatService.SendMessageAsync(message);
        
        // Cache'i güncelle
        if (savedMessage.ReceiverId.HasValue)
        {
            // Kişisel mesaj
            var key = GetConversationKey(savedMessage.SenderId, savedMessage.ReceiverId.Value);
            _messagesByUser.AddOrUpdate(key, 
                new List<ChatMessageModel> { savedMessage },
                (k, existing) => 
                {
                    existing.Add(savedMessage);
                    return existing;
                });
            
            // Chat user listesini güncelle
            await UpdateChatUserLastMessage(savedMessage);
        }
        else if (savedMessage.GroupId.HasValue)
        {
            // Grup mesajı
            var key = $"group-{savedMessage.GroupId.Value}";
            _messagesByUser.AddOrUpdate(key, 
                new List<ChatMessageModel> { savedMessage },
                (k, existing) => 
                {
                    existing.Add(savedMessage);
                    return existing;
                });
            
            // Grup bilgisini güncelle
            await UpdateGroupLastMessage(savedMessage);
        }
        
        _eventBus.PublishMessage(savedMessage);
    }
    
    // Sadece cache'e mesaj ekle (database'e kaydetmeden)
    // Sadece cache'e mesaj ekle (database'e kaydetmeden)
    public async Task AddMessageToCacheAsync(ChatMessageModel message)
    {
        if (message.ReceiverId.HasValue)
        {
            // Kişisel mesaj
            var key = GetConversationKey(message.SenderId, message.ReceiverId.Value);
            _messagesByUser.AddOrUpdate(key, 
                new List<ChatMessageModel> { message },
                (k, existing) => 
                {
                    // Duplicate kontrolü
                    if (!existing.Any(m => m.Id == message.Id))
                    {
                        existing.Add(message);
                    }
                    return existing.OrderBy(m => m.SentAt).ToList();
                });
        }
        else if (message.GroupId.HasValue)
        {
            // Grup mesajı
            var key = $"group-{message.GroupId.Value}";
            _messagesByUser.AddOrUpdate(key, 
                new List<ChatMessageModel> { message },
                (k, existing) => 
                {
                    // Duplicate kontrolü
                    if (!existing.Any(m => m.Id == message.Id))
                    {
                        existing.Add(message);
                    }
                    return existing.OrderBy(m => m.SentAt).ToList();
                });
        }
    }

    // Grup mesajlarını okundu olarak işaretle
    public async Task MarkGroupMessagesAsReadAsync(Guid currentUserId, Guid groupId)
    {
        await _chatService.MarkGroupMessagesAsReadAsync(currentUserId, groupId);
    
        if (_chatUsers.TryGetValue(groupId, out var group))
        {
            group.UnreadMessageCount = 0;
            _eventBus.PublishUnreadCountChange(groupId, 0);
        }
    }
    
    // Mesajları okundu olarak işaretle
    public async Task MarkMessagesAsReadAsync(Guid currentUserId, Guid otherUserId)
    {
        await _chatService.MarkMessagesAsReadAsync(currentUserId, otherUserId);
        
        if (_chatUsers.TryGetValue(otherUserId, out var user))
        {
            user.UnreadMessageCount = 0;
            _eventBus.PublishUnreadCountChange(otherUserId, 0);
        }
    }
    
    private async Task UpdateChatUserLastMessage(ChatMessageModel message)
    {
        var targetUserId = message.SenderId != message.ReceiverId!.Value ? message.ReceiverId.Value : message.SenderId;
        if (_chatUsers.TryGetValue(targetUserId, out var user))
        {
            user.LastMessage = message.Content;
            user.LastMessageTime = message.SentAt;
            
            if (message.SenderId != message.ReceiverId!.Value)
            {
                user.UnreadMessageCount++;
                _eventBus.PublishUnreadCountChange(user.Id, user.UnreadMessageCount);
            }
        }
    }
    
    private async Task UpdateGroupLastMessage(ChatMessageModel message)
    {
        if (_chatUsers.TryGetValue(message.GroupId!.Value, out var group))
        {
            group.LastMessage = message.Content;
            group.LastMessageTime = message.SentAt;
            
        }
    }
    
    private string GetConversationKey(Guid userId1, Guid userId2)
    {
        var ids = new[] { userId1, userId2 }.OrderBy(id => id).ToArray();
        return $"{ids[0]}-{ids[1]}";
    }
    
}