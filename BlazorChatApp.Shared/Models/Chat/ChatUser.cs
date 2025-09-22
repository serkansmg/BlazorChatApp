namespace BlazorChatApp.Models.Chat;

public class ChatUser
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public UserType UserType { get; set; }
    public int UnreadMessageCount { get; set; }
    public DateTime LastMessageTime { get; set; }
    public string LastMessage { get; set; } = "";
}