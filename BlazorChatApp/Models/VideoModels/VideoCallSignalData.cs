namespace BlazorChatApp.Models.Video;

public class VideoCallSignalData
{
    public string Type { get; set; } = "";
    public string SenderId { get; set; } = "";
    public string? Data { get; set; }
    public DateTime Timestamp { get; set; }
}