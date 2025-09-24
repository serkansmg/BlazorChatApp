using BlazorChatApp.Models.Chat;
using BlazorChatApp.Shared.Models.VideoModels;

namespace BlazorChatApp.Services;

public class EventBus
{
    public event Action<ChatMessageModel>? MessageReceived;
    public event Action<ChatUser>? UserStatusChanged;
    public event Action<Guid, int>? UnreadCountChanged;
    public event Action<object>? FriendRequestReceived;
    public event Action<object>? FriendRequestAccepted;
    public event Action<object>? VideoCallSignalReceived;
    
    
    public event Action<string>? VideoCallStateChanged;
    public event Action<string>? VideoCallError;
    public event Action<List<MediaDevice>, List<MediaDevice>>? MediaDevicesLoaded;
    
    public void PublishVideoCallStateChange(string state)
    {
        VideoCallStateChanged?.Invoke(state);
        
    }

    public void PublishVideoCallError(string error)
    {
        VideoCallError?.Invoke(error);
    }
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
    
    public void PublishVideoCallSignal(object signalData)
    {
        Console.WriteLine($"EventBus.PublishVideoCallSignal called with: {signalData}");
        Console.WriteLine($"Subscriber count: {VideoCallSignalReceived?.GetInvocationList()?.Length ?? 0}");
        VideoCallSignalReceived?.Invoke(signalData);
        Console.WriteLine("VideoCallSignalReceived event invoked");
    }
    
    public void PublishMediaDevicesLoaded(List<MediaDevice> videoDevices, List<MediaDevice> audioDevices)
    {
        MediaDevicesLoaded?.Invoke(videoDevices, audioDevices);
    }
}