using BlazorChatApp.Models.Video;
using System.Text.Json;
using BlazorChatApp.Models;
using Microsoft.Extensions.Options;

namespace BlazorChatApp.Services;

public class JanusVideoService : IVideoConferenceService
{
    public async Task<VideoRoomInfo> CreateRoomAsync(string roomName, int maxParticipants, string createdByUserId)
    {
        throw new NotImplementedException();
    }

    public async Task<VideoRoomInfo> JoinRoomAsync(string roomId, string userId, string displayName)
    {
        throw new NotImplementedException();
    }

    public async Task LeaveRoomAsync(string roomId, string userId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<VideoParticipant>> GetRoomParticipantsAsync(string roomId)
    {
        throw new NotImplementedException();
    }

    public async Task<VideoRoomInfo?> GetRoomInfoAsync(string roomId)
    {
        throw new NotImplementedException();
    }

    public async Task<List<VideoRoomInfo>> GetActiveRoomsAsync()
    {
        throw new NotImplementedException();
    }

    public async Task SendVideoSignalAsync(string roomId, VideoSignalData signalData)
    {
        throw new NotImplementedException();
    }

    public async Task DeleteRoomAsync(string roomId, string requestedByUserId)
    {
        throw new NotImplementedException();
    }

    public async Task ToggleParticipantMediaAsync(string roomId, string userId, bool isMuted, bool isVideoEnabled)
    {
        throw new NotImplementedException();
    }

    public async Task UpdateParticipantRoleAsync(string roomId, string userId, VideoParticipantRole role, string requestedByUserId)
    {
        throw new NotImplementedException();
    }

    public async Task KickParticipantAsync(string roomId, string userId, string requestedByUserId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> IsRoomExistsAsync(string roomId)
    {
        throw new NotImplementedException();
    }

    public async Task<bool> IsUserInRoomAsync(string roomId, string userId)
    {
        throw new NotImplementedException();
    }

    public async Task InitializeAsync()
    {
        throw new NotImplementedException();
    }
}

// Janus API response models
internal class JanusResponse
{
    public string Janus { get; set; } = "";
    public JanusData Data { get; set; } = new();
}

internal class JanusData
{
    public long Id { get; set; }
}