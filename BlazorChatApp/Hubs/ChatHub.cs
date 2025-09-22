using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using BlazorChatApp.Models.Chat;
using BlazorChatApp.Services;

namespace BlazorChatApp.Hubs;

[Authorize]
public class ChatHub : Hub
{
    private readonly ChatService _chatService;

    public ChatHub(ChatService chatService)
    {
        _chatService = chatService;
    }

    // Kullanıcı bağlandığında
    public override async Task OnConnectedAsync()
    {
        var userId = Context.UserIdentifier;
        Console.WriteLine($"User connected: {userId}");

        if (!string.IsNullOrEmpty(userId))
        {
            var groupName = $"user-{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
            Console.WriteLine($"User {userId} added to group: {groupName}");
            Console.WriteLine($"ConnectionId: {Context.ConnectionId}"); // DEBUG eklendi

            // Online durumunu güncelle
            await Clients.Others.SendAsync("UserOnline", userId);
        }
        else
        {
            Console.WriteLine("UserId is null or empty!"); // DEBUG eklendi
        }

        await base.OnConnectedAsync();
    }

    // Kullanıcı bağlantısını kestiğinde
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = Context.UserIdentifier;
        if (!string.IsNullOrEmpty(userId))
        {
            await Clients.Others.SendAsync("UserOffline", userId);
        }

        await base.OnDisconnectedAsync(exception);
    }


    // Text mesajlar için (mevcut)
    public async Task SendMessageToUser(string receiverId, string message)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId)) return;

        var chatMessage = new ChatMessageModel
        {
            Id = Guid.NewGuid(),
            SenderId = Guid.Parse(senderId),
            ReceiverId = Guid.Parse(receiverId),
            Content = message,
            MessageType = MessageType.Text,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await _chatService.SendMessageAsync(chatMessage);
        await Clients.Group($"user-{receiverId}").SendAsync("ReceiveMessage", chatMessage);
        await Clients.Group($"user-{senderId}").SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task SendMessageToGroup(string groupId, string message)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId)) return;

        var chatMessage = new ChatMessageModel
        {
            Id = Guid.NewGuid(),
            SenderId = Guid.Parse(senderId),
            GroupId = Guid.Parse(groupId),
            Content = message,
            MessageType = MessageType.Text,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await _chatService.SendMessageAsync(chatMessage);
        await Clients.Group($"group-{groupId}").SendAsync("ReceiveGroupMessage", chatMessage);
    }

 
    public async Task SendMediaMessageToUser(string receiverId, string content, string messageType, string fileUrl,
        string fileName, long fileSize, string mimeType)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId)) return;

        var chatMessage = new ChatMessageModel
        {
            Id = Guid.NewGuid(),
            SenderId = Guid.Parse(senderId),
            ReceiverId = Guid.Parse(receiverId),
            Content = content,
            MessageType = Enum.Parse<MessageType>(messageType),
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await _chatService.SendMessageAsync(chatMessage);
        await Clients.Group($"user-{receiverId}").SendAsync("ReceiveMessage", chatMessage);
        await Clients.Group($"user-{senderId}").SendAsync("ReceiveMessage", chatMessage);
    }

    public async Task SendMediaMessageToGroup(string groupId, string content, string messageType, string fileUrl,
        string fileName, long fileSize, string mimeType)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId)) return;

        var chatMessage = new ChatMessageModel
        {
            Id = Guid.NewGuid(),
            SenderId = Guid.Parse(senderId),
            GroupId = Guid.Parse(groupId),
            Content = content,
            MessageType = Enum.Parse<MessageType>(messageType),
            FileUrl = fileUrl,
            FileName = fileName,
            FileSize = fileSize,
            MimeType = mimeType,
            SentAt = DateTime.UtcNow,
            IsRead = false
        };

        await _chatService.SendMessageAsync(chatMessage);
        await Clients.Group($"group-{groupId}").SendAsync("ReceiveGroupMessage", chatMessage);
    }

    // Gruba katıl
    public async Task JoinGroup(string groupId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"group-{groupId}");
    }

    // Gruptan ayrıl
    public async Task LeaveGroup(string groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"group-{groupId}");
    }

    // Mesaj okundu bildirimi
    public async Task MarkAsRead(string messageId)
    {
        // Database'de mesajı okundu olarak işaretle
        // Implementation gerekirse ekleyeceğiz
    }

    // Yazıyor bildirimi
    public async Task SendTypingNotification(string receiverId)
    {
        var senderId = Context.UserIdentifier;
        await Clients.Group($"user-{receiverId}").SendAsync("UserTyping", senderId);
    }
    
    // Video call signaling
    public async Task SendVideoCallSignal(string receiverId, string signalType, string? data = null)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId)) return;

        var signalData = new
        {
            Type = signalType,
            SenderId = senderId,
            Data = data,
            Timestamp = DateTime.UtcNow
        };

        // Alıcıya gönder
        await Clients.Group($"user-{receiverId}").SendAsync("ReceiveVideoCallSignal", signalData);
    
        Console.WriteLine($"Video signal sent: {signalType} from {senderId} to {receiverId}");
    }

    // Arkadaşlık isteği gönder
    public async Task SendFriendRequest(string receiverId, string senderName, string? message = null)
    {
        var senderId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(senderId))
        {
            Console.WriteLine("SenderId is null or empty");
            return;
        }

        Console.WriteLine(
            $"SendFriendRequest - SenderId: {senderId}, ReceiverId: {receiverId}, SenderName: {senderName}");

        // Alıcıya real-time bildirim gönder
        var groupName = $"user-{receiverId}";
        Console.WriteLine($"Sending to group: {groupName}");

        var data = new
        {
            SenderId = senderId,
            SenderName = senderName,
            Message = message,
            RequestedAt = DateTime.UtcNow
        };

        await Clients.Group($"user-{receiverId}").SendAsync("FriendRequestReceived", data);
        Console.WriteLine($"FriendRequestReceived event sent to group: user-{receiverId}");
    }

// Arkadaşlık isteği kabul edildi
    public async Task AcceptFriendRequest(string senderId, string accepterName)
    {
        var accepterId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(accepterId)) return;

        // İstek gönderene bildirim
        await Clients.Group($"user-{senderId}").SendAsync("FriendRequestAccepted", new
        {
            AccepterId = accepterId,
            AccepterName = accepterName,
            AcceptedAt = DateTime.UtcNow
        });
    }
// MediaSoup transport connect
    public async Task ConnectTransport(string transportId, object dtlsParameters)
    {
        var senderId = Context.UserIdentifier;
        await Clients.Group($"user-{senderId}").SendAsync("TransportConnected", new { transportId, dtlsParameters });
    }

// MediaSoup produce
    public async Task ProduceMedia(string transportId, string kind, object rtpParameters)
    {
        var senderId = Context.UserIdentifier;
        await Clients.Group($"user-{senderId}").SendAsync("MediaProduced", new { transportId, kind, rtpParameters });
    }
// Arkadaşlık isteği reddedildi
    public async Task RejectFriendRequest(string senderId)
    {
        var rejecterId = Context.UserIdentifier;
        if (string.IsNullOrEmpty(rejecterId)) return;

        await Clients.Group($"user-{senderId}").SendAsync("FriendRequestRejected", new
        {
            RejecterId = rejecterId,
            RejectedAt = DateTime.UtcNow
        });
    }
}