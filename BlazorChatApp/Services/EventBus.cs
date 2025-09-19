using BlazorChatApp.Models.Chat;

namespace BlazorChatApp.Services;

public class EventBus
{
    public event Action<ChatMessageModel>? MessageReceived;
    public event Action<ChatUser>? UserStatusChanged;
    public event Action<Guid, int>? UnreadCountChanged;
    public event Action<object>? FriendRequestReceived;
    public event Action<object>? FriendRequestAccepted;
    
    public void PublishFriendRequestReceived(object data)
    {
        FriendRequestReceived?.Invoke(data);
    }
    
    public void PublishFriendRequestAccepted(object data)
    {
        FriendRequestAccepted?.Invoke(data);
    }
    public void PublishMessage(ChatMessageModel message)
    {
        MessageReceived?.Invoke(message);
    }
    
    public void PublishUserStatusChange(ChatUser user)
    {
        UserStatusChanged?.Invoke(user);
    }
    
    public void PublishUnreadCountChange(Guid userId, int count)
    {
        UnreadCountChanged?.Invoke(userId, count);
    }
}