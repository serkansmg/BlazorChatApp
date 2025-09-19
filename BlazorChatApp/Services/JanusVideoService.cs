using BlazorChatApp.Models.Video;

namespace BlazorChatApp.Services;

public class JanusVideoService:IVideoConferenceService
{
    public Task<VideoRoomInfo> CreateRoomAsync(string roomName, int maxParticipants, string createdByUserId)
    {
        throw new NotImplementedException();
    }

    public Task<VideoRoomInfo> JoinRoomAsync(string roomId, string userId, string displayName)
    {
        throw new NotImplementedException();
    }

    public Task LeaveRoomAsync(string roomId, string userId)
    {
        throw new NotImplementedException();
    }

    public Task<List<VideoParticipant>> GetRoomParticipantsAsync(string roomId)
    {
        throw new NotImplementedException();
    }

    public Task<VideoRoomInfo?> GetRoomInfoAsync(string roomId)
    {
        throw new NotImplementedException();
    }

    public Task<List<VideoRoomInfo>> GetActiveRoomsAsync()
    {
        throw new NotImplementedException();
    }

    public Task SendVideoSignalAsync(string roomId, VideoSignalData signalData)
    {
        throw new NotImplementedException();
    }

    public Task DeleteRoomAsync(string roomId, string requestedByUserId)
    {
        throw new NotImplementedException();
    }

    public Task ToggleParticipantMediaAsync(string roomId, string userId, bool isMuted, bool isVideoEnabled)
    {
        throw new NotImplementedException();
    }

    public Task UpdateParticipantRoleAsync(string roomId, string userId, VideoParticipantRole role, string requestedByUserId)
    {
        throw new NotImplementedException();
    }

    public Task KickParticipantAsync(string roomId, string userId, string requestedByUserId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsRoomExistsAsync(string roomId)
    {
        throw new NotImplementedException();
    }

    public Task<bool> IsUserInRoomAsync(string roomId, string userId)
    {
        throw new NotImplementedException();
    }
}