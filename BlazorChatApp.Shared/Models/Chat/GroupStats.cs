namespace BlazorChatApp.Models.Chat;

public class GroupStats
{
    public int MemberCount { get; set; }
    public int MessageCount { get; set; }
    public DateTime? LastMessageAt { get; set; }
}