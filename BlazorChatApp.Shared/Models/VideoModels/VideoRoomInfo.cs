namespace BlazorChatApp.Models.Video;

public class VideoRoomInfo
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string WebSocketUrl { get; set; } = "";
    public string ApiUrl { get; set; } = "";
    public int MaxParticipants { get; set; }
    public int CurrentParticipantCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Dictionary<string, object> ConnectionParams { get; set; } = new();
    public VideoRoomStatus Status { get; set; }
}