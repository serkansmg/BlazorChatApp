namespace BlazorChatApp.Models.Video;

public class VideoParticipant
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string AvatarUrl { get; set; } = "";
    public DateTime JoinedAt { get; set; }
    public bool IsMuted { get; set; }
    public bool IsVideoEnabled { get; set; }
    public bool IsAudioEnabled { get; set; }
    public bool IsScreenSharing { get; set; }
    public VideoParticipantRole Role { get; set; }
    public Dictionary<string, object> MediaSettings { get; set; } = new();
}