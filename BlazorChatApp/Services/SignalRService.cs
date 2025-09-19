using Microsoft.JSInterop;
using BlazorChatApp.Models.Chat;

namespace BlazorChatApp.Services;

public class SignalRService : IMessageService,IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly EventBus _eventBus;
    private DotNetObjectReference<SignalRService>? _dotNetRef;
    private bool _isInitialized = false;  
    private readonly object _initLock = new object();
    
    public SignalRService(IJSRuntime jsRuntime, EventBus eventBus)
    {
        _jsRuntime = jsRuntime;
        _eventBus = eventBus;
    }
    
    public async Task InitializeAsync()
    {
        lock (_initLock)
        {
            if (_isInitialized) return; // Zaten initialize edilmişse çık
            _isInitialized = true;
        }
        
        try
        {
            _dotNetRef = DotNetObjectReference.Create(this);
            await _jsRuntime.InvokeVoidAsync("initializeSignalR", _dotNetRef);
        }
        catch (Exception ex)
        {
            // Hata durumunda flag'i resetle
            lock (_initLock)
            {
                _isInitialized = false;
            }
            throw;
        }
    }
    
    public async Task SendMessageToUserAsync(string receiverId, string message)
    {
        await _jsRuntime.InvokeVoidAsync("sendMessageToUser", receiverId, message);
    }
    
    public async Task SendMessageToGroupAsync(string groupId, string message)
    {
        await _jsRuntime.InvokeVoidAsync("sendMessageToGroup", groupId, message);
    }
    
    public async Task JoinGroupAsync(string groupId)
    {
        await _jsRuntime.InvokeVoidAsync("joinGroup", groupId);
    }
    
    public async Task SendTypingNotificationAsync(string receiverId)
    {
        await _jsRuntime.InvokeVoidAsync("sendTypingNotification", receiverId);
    }
    
    // JavaScript'ten çağrılacak JSInvokable metodlar
    [JSInvokable]
    public void OnMessageReceived(ChatMessageModel message)
    {
        _eventBus.PublishMessage(message);
    }
    
    [JSInvokable]
    public void OnGroupMessageReceived(ChatMessageModel message)
    {
        _eventBus.PublishMessage(message);
    }
    
    [JSInvokable]
    public void OnUserOnline(string userId)
    {
        if (Guid.TryParse(userId, out var userGuid))
        {
            // User online event publish et
            Console.WriteLine($"User {userId} is online");
        }
    }
    
    [JSInvokable]
    public void OnUserOffline(string userId)
    {
        if (Guid.TryParse(userId, out var userGuid))
        {
            // User offline event publish et
            Console.WriteLine($"User {userId} is offline");
        }
    }
    
    [JSInvokable]
    public void OnUserTyping(string userId)
    {
        if (Guid.TryParse(userId, out var userGuid))
        {
            // Typing indicator göster
            Console.WriteLine($"User {userId} is typing");
        }
    }
    
    [JSInvokable]
    public void OnConnectionStatusChanged(bool isConnected)
    {
        Console.WriteLine($"SignalR connection status: {isConnected}");
    }
    
    [JSInvokable]
    public void OnFriendRequestReceived(object data)
    {
        // Toast notification göster
        Console.WriteLine($"OnFriendRequestReceived called with data: {data}");
        _eventBus.PublishFriendRequestReceived(data);
    }

    [JSInvokable]  
    public void OnFriendRequestAccepted(object data)
    {
        // Toast notification göster
        _eventBus.PublishFriendRequestAccepted(data);
    }

    [JSInvokable]
    public void OnFriendRequestRejected(object data)
    {
        // Bildirim göster
        Console.WriteLine("Friend request was rejected");
    }

// SignalR metodları ekleyin
    public async Task SendFriendRequestAsync(string receiverId, string senderName, string? message = null)
    {
        await _jsRuntime.InvokeVoidAsync("sendFriendRequest", receiverId, senderName, message);
    }

    public async Task AcceptFriendRequestAsync(string senderId, string accepterName)
    {
        await _jsRuntime.InvokeVoidAsync("acceptFriendRequest", senderId, accepterName);
    }
     
    public async Task SendMediaMessageToUserAsync(string receiverId, string content, string messageType, string fileUrl, string fileName, long fileSize, string mimeType)
    {
        await _jsRuntime.InvokeVoidAsync("sendMediaMessageToUser", receiverId, content, messageType, fileUrl, fileName, fileSize, mimeType);
    }

    public async Task SendMediaMessageToGroupAsync(string groupId, string content, string messageType, string fileUrl, string fileName, long fileSize, string mimeType)
    {
        await _jsRuntime.InvokeVoidAsync("sendMediaMessageToGroup", groupId, content, messageType, fileUrl, fileName, fileSize, mimeType);
    }
    public async Task SendVideoCallSignalAsync(string receiverId, string signalType, string? data = null)
    {
        await _jsRuntime.InvokeVoidAsync("sendVideoCallSignal", receiverId, signalType, data);
    }
    
    [JSInvokable]
    public void OnVideoCallSignalReceived(object signalData)
    {
        Console.WriteLine($"Video call signal received: {signalData}");
        _eventBus.PublishVideoCallSignal(signalData);
    }
    
    public async ValueTask DisposeAsync()
    {
        lock (_initLock)
        {
            _isInitialized = false;
        }
        _dotNetRef?.Dispose();
    }
}