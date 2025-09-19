namespace BlazorChatApp.Services;

public interface IMessageService
{
    Task InitializeAsync();
    
    // Text mesajlar
    Task SendMessageToUserAsync(string receiverId, string message);
    Task SendMessageToGroupAsync(string groupId, string message);
    
    // Medya mesajlar
    Task SendMediaMessageToUserAsync(string receiverId, string content, string messageType, string fileUrl, string fileName, long fileSize, string mimeType);
    Task SendMediaMessageToGroupAsync(string groupId, string content, string messageType, string fileUrl, string fileName, long fileSize, string mimeType);
    
    // Grup işlemleri
    Task JoinGroupAsync(string groupId);
    Task SendTypingNotificationAsync(string receiverId);
    
    // Arkadaşlık işlemleri
    Task SendFriendRequestAsync(string receiverId, string senderName, string? message = null);
    Task AcceptFriendRequestAsync(string senderId, string accepterName);
    // Video call işlemleri ← YENİ
    Task SendVideoCallSignalAsync(string receiverId, string signalType, string? data = null);
}