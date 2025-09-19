namespace BlazorChatApp.Models.Video;

public class VideoSignalData
{
    public string Type { get; set; } = ""; // offer, answer, ice-candidate, etc.
    public string FromUserId { get; set; } = "";
    public string ToUserId { get; set; } = ""; // empty for broadcast
    public object Data { get; set; } = new();
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}