using BlazorChatApp.Models.Video;
using System.Text.Json;

namespace BlazorChatApp.Services;

public class MediaSoupVideoService : IVideoConferenceService
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<MediaSoupVideoService> _logger;
    private readonly string _mediaSoupApiUrl;

    public MediaSoupVideoService(HttpClient httpClient, IConfiguration configuration, ILogger<MediaSoupVideoService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _logger = logger;
        _mediaSoupApiUrl = configuration["MediaSoup:ApiUrl"] ?? "http://localhost:3000";
    }

    public async Task<VideoRoomInfo> CreateRoomAsync(string roomName, int maxParticipants, string createdByUserId)
    {
        var payload = new
        {
            roomName,
            maxParticipants,
            createdBy = createdByUserId
        };

        var response = await _httpClient.PostAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MediaSoupRoomResponse>();
        
        return new VideoRoomInfo
        {
            RoomId = result.RoomId,
            RoomName = roomName,
            WebSocketUrl = $"{_mediaSoupApiUrl.Replace("http", "ws")}/ws",
            ApiUrl = _mediaSoupApiUrl,
            MaxParticipants = maxParticipants,
            CurrentParticipantCount = 0,
            CreatedAt = DateTime.UtcNow,
            Status = VideoRoomStatus.Active,
            ConnectionParams = new Dictionary<string, object>
            {
                { "roomId", result.RoomId },
                { "serverUrl", _mediaSoupApiUrl }
            }
        };
    }

    public async Task<VideoRoomInfo> JoinRoomAsync(string roomId, string userId, string displayName)
    {
        var payload = new
        {
            roomId,
            userId,
            displayName
        };

        var response = await _httpClient.PostAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/join", payload);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<MediaSoupJoinResponse>();

        // Send transport oluştur
        var sendTransportResponse = await _httpClient.PostAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/transports", 
            new { userId, direction = "send" });
        var sendTransport = await sendTransportResponse.Content.ReadFromJsonAsync<MediaSoupTransportResponse>();

        // Recv transport oluştur  
        var recvTransportResponse = await _httpClient.PostAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/transports", 
            new { userId, direction = "recv" });
        var recvTransport = await recvTransportResponse.Content.ReadFromJsonAsync<MediaSoupTransportResponse>();

        return new VideoRoomInfo
        {
            RoomId = roomId,
            WebSocketUrl = $"{_mediaSoupApiUrl.Replace("http", "ws")}/ws",
            ApiUrl = _mediaSoupApiUrl,
            ConnectionParams = new Dictionary<string, object>
            {
                { "routerRtpCapabilities", result.RouterRtpCapabilities },
                { "sendTransport", sendTransport },
                { "recvTransport", recvTransport },
                { "peerId", result.PeerId }
            }
        };
    }
    
    public async Task LeaveRoomAsync(string roomId, string userId)
    {
        await _httpClient.PostAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/leave", 
            JsonContent.Create(new { userId }));
    }

    public async Task<List<VideoParticipant>> GetRoomParticipantsAsync(string roomId)
    {
        var response = await _httpClient.GetAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/participants");
        response.EnsureSuccessStatusCode();

        var participants = await response.Content.ReadFromJsonAsync<List<MediaSoupParticipant>>();
        
        return participants.Select(p => new VideoParticipant
        {
            UserId = p.UserId,
            DisplayName = p.DisplayName,
            JoinedAt = p.JoinedAt,
            IsMuted = p.IsMuted,
            IsVideoEnabled = p.IsVideoEnabled,
            Role = VideoParticipantRole.Participant
        }).ToList();
    }

    public async Task<VideoRoomInfo?> GetRoomInfoAsync(string roomId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}");
            response.EnsureSuccessStatusCode();

            var room = await response.Content.ReadFromJsonAsync<MediaSoupRoomInfo>();
            
            return new VideoRoomInfo
            {
                RoomId = room.RoomId,
                RoomName = room.RoomName,
                MaxParticipants = room.MaxParticipants,
                CurrentParticipantCount = room.ParticipantCount,
                CreatedAt = room.CreatedAt,
                Status = VideoRoomStatus.Active,
                WebSocketUrl = $"{_mediaSoupApiUrl.Replace("http", "ws")}/ws",
                ApiUrl = _mediaSoupApiUrl
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<VideoRoomInfo>> GetActiveRoomsAsync()
    {
        var response = await _httpClient.GetAsync($"{_mediaSoupApiUrl}/api/rooms");
        response.EnsureSuccessStatusCode();

        var rooms = await response.Content.ReadFromJsonAsync<List<MediaSoupRoomInfo>>();
        
        return rooms.Select(r => new VideoRoomInfo
        {
            RoomId = r.RoomId,
            RoomName = r.RoomName,
            MaxParticipants = r.MaxParticipants,
            CurrentParticipantCount = r.ParticipantCount,
            CreatedAt = r.CreatedAt,
            Status = VideoRoomStatus.Active
        }).ToList();
    }

    public async Task SendVideoSignalAsync(string roomId, VideoSignalData signalData)
    {
        var payload = new
        {
            roomId,
            type = signalData.Type,
            fromUserId = signalData.FromUserId,
            toUserId = signalData.ToUserId,
            data = signalData.Data
        };

        await _httpClient.PostAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/signal", payload);
    }

    public async Task DeleteRoomAsync(string roomId, string requestedByUserId)
    {
        await _httpClient.DeleteAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}");
    }

    public async Task ToggleParticipantMediaAsync(string roomId, string userId, bool isMuted, bool isVideoEnabled)
    {
        var payload = new { userId, isMuted, isVideoEnabled };
        await _httpClient.PutAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/media", payload);
    }

    public async Task UpdateParticipantRoleAsync(string roomId, string userId, VideoParticipantRole role, string requestedByUserId)
    {
        var payload = new { userId, role = role.ToString(), requestedBy = requestedByUserId };
        await _httpClient.PutAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/role", payload);
    }

    public async Task KickParticipantAsync(string roomId, string userId, string requestedByUserId)
    {
        var payload = new { userId, requestedBy = requestedByUserId };
        await _httpClient.PostAsJsonAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}/kick", payload);
    }

    public async Task<bool> IsRoomExistsAsync(string roomId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_mediaSoupApiUrl}/api/rooms/{roomId}");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> IsUserInRoomAsync(string roomId, string userId)
    {
        try
        {
            var participants = await GetRoomParticipantsAsync(roomId);
            return participants.Any(p => p.UserId == userId);
        }
        catch
        {
            return false;
        }
    }
}

// MediaSoup API response modelleri
internal class MediaSoupRoomResponse
{
    public string RoomId { get; set; } = "";
}

internal class MediaSoupJoinResponse
{
    public object RouterRtpCapabilities { get; set; } = new();
    public string PeerId { get; set; } = "";
}

internal class MediaSoupTransportResponse
{
    public string Id { get; set; } = "";
    public object IceParameters { get; set; } = new();
    public object[] IceCandidates { get; set; } = Array.Empty<object>();
    public object DtlsParameters { get; set; } = new();
}

internal class MediaSoupParticipant
{
    public string UserId { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public DateTime JoinedAt { get; set; }
    public bool IsMuted { get; set; }
    public bool IsVideoEnabled { get; set; }
}

internal class MediaSoupRoomInfo
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public int MaxParticipants { get; set; }
    public int ParticipantCount { get; set; }
    public DateTime CreatedAt { get; set; }
}