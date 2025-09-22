
using BlazorChatApp.Models.Video;

namespace BlazorChatApp.Services;

public interface IVideoConferenceService
{
    Task<VideoRoomInfo> CreateRoomAsync(string roomName, int maxParticipants, string createdByUserId);
    Task<VideoRoomInfo> JoinRoomAsync(string roomId, string userId, string displayName);
    Task LeaveRoomAsync(string roomId, string userId);
    Task<List<VideoParticipant>> GetRoomParticipantsAsync(string roomId);
    Task<VideoRoomInfo?> GetRoomInfoAsync(string roomId);
    Task<List<VideoRoomInfo>> GetActiveRoomsAsync();
    Task SendVideoSignalAsync(string roomId, VideoSignalData signalData);
    Task DeleteRoomAsync(string roomId, string requestedByUserId);
    Task ToggleParticipantMediaAsync(string roomId, string userId, bool isMuted, bool isVideoEnabled);
    Task UpdateParticipantRoleAsync(string roomId, string userId, VideoParticipantRole role, string requestedByUserId);
    Task KickParticipantAsync(string roomId, string userId, string requestedByUserId);
    Task<bool> IsRoomExistsAsync(string roomId);
    Task<bool> IsUserInRoomAsync(string roomId, string userId);
    Task InitializeAsync();
}