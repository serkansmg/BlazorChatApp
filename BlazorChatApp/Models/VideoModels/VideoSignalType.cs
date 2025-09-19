namespace BlazorChatApp.Models.Video;

public enum VideoSignalType
{
    Offer,
    Answer,
    IceCandidate,
    JoinRoom,
    LeaveRoom,
    MediaToggle,
    ScreenShare
}