using BlazorChatApp.Models.Video;
using System.Text.Json;
using BlazorChatApp.Models;
using Microsoft.Extensions.Options;

namespace BlazorChatApp.Services;

public class JanusVideoService : IVideoConferenceService
{
    private readonly HttpClient _httpClient;
    private readonly JanusSettings _janusSettings;
    private readonly ILogger<JanusVideoService> _logger;
    private readonly string _janusApiUrl;

    public JanusVideoService(HttpClient httpClient, IOptions<JanusSettings> janusOptions,
        ILogger<JanusVideoService> logger)
    {
        _httpClient = httpClient;
        _janusSettings = janusOptions.Value;
        _logger = logger;
        _janusApiUrl = _janusSettings.ApiUrl;
    }

    public async Task<VideoRoomInfo> CreateRoomAsync(string roomName, int maxParticipants, string createdByUserId)
    {
        // Janus session oluştur
        var sessionResponse = await CreateJanusSession();
        var sessionId = sessionResponse.Data.Id;

        // VideoRoom plugin attach et
        var pluginResponse = await AttachVideoRoomPlugin(sessionId);
        var handleId = pluginResponse.Data.Id;

        // Room oluştur
        var roomId = GenerateRoomId();
        var createRoomRequest = new
        {
            janus = "message",
            session_id = sessionId,
            handle_id = handleId,
            body = new
            {
                request = "create",
                room = roomId,
                description = roomName,
                publishers = maxParticipants,
                is_private = false,
                audiolevel_event = true,
                videolevel_event = true
            }
        };

        var response = await _httpClient.PostAsJsonAsync($"{_janusApiUrl}/{sessionId}/{handleId}", createRoomRequest);

        return new VideoRoomInfo
        {
            RoomId = roomId.ToString(),
            RoomName = roomName,
            WebSocketUrl = _janusApiUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws",
            ApiUrl = _janusApiUrl,
            MaxParticipants = maxParticipants,
            CurrentParticipantCount = 0,
            CreatedAt = DateTime.UtcNow,
            Status = VideoRoomStatus.Active,
            ConnectionParams = new Dictionary<string, object>
            {
                { "sessionId", sessionId },
                { "handleId", handleId },
                { "roomId", roomId },
                { "janusUrl", _janusApiUrl }
            }
        };
    }

    public async Task<VideoRoomInfo> JoinRoomAsync(string roomId, string userId, string displayName)
    {
        var sessionResponse = await CreateJanusSession();
        var sessionId = sessionResponse.Data.Id;

        var pluginResponse = await AttachVideoRoomPlugin(sessionId);
        var handleId = pluginResponse.Data.Id;

        return new VideoRoomInfo
        {
            RoomId = roomId,
            WebSocketUrl = _janusApiUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/ws",
            ApiUrl = _janusApiUrl,
            ConnectionParams = new Dictionary<string, object>
            {
                { "sessionId", sessionId },
                { "handleId", handleId },
                { "roomId", int.Parse(roomId) },
                { "displayName", displayName },
                { "janusUrl", _janusApiUrl }
            }
        };
    }

    public async Task LeaveRoomAsync(string roomId, string userId)
    {
        // Janus'ta user-specific session takibi yapmak gerekiyor
        // Şimdilik placeholder
        _logger.LogInformation($"User {userId} left room {roomId}");
    }

    public async Task<List<VideoParticipant>> GetRoomParticipantsAsync(string roomId)
    {
        // Janus VideoRoom participants list
        // Placeholder - gerçek implementasyon için Janus API çağrısı gerekli
        return new List<VideoParticipant>();
    }

    public async Task<VideoRoomInfo?> GetRoomInfoAsync(string roomId)
    {
        try
        {
            // Janus room exists check
            return new VideoRoomInfo
            {
                RoomId = roomId,
                Status = VideoRoomStatus.Active
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task<List<VideoRoomInfo>> GetActiveRoomsAsync()
    {
        return new List<VideoRoomInfo>();
    }

    public async Task SendVideoSignalAsync(string roomId, VideoSignalData signalData)
    {
        // Janus signaling - bizim SignalR üzerinden yapacağız
        _logger.LogInformation($"Video signal: {signalData.Type}");
    }

    public async Task DeleteRoomAsync(string roomId, string requestedByUserId)
    {
        // Janus room destroy
        _logger.LogInformation($"Room {roomId} deleted");
    }

    // Diğer interface metodları placeholder...
    public async Task ToggleParticipantMediaAsync(string roomId, string userId, bool isMuted, bool isVideoEnabled)
    {
    }

    public async Task UpdateParticipantRoleAsync(string roomId, string userId, VideoParticipantRole role,
        string requestedByUserId)
    {
    }

    public async Task KickParticipantAsync(string roomId, string userId, string requestedByUserId)
    {
    }

    public async Task<bool> IsRoomExistsAsync(string roomId) => true;
    public async Task<bool> IsUserInRoomAsync(string roomId, string userId) => false;

    // Janus helper metodları
    private async Task<JanusResponse> CreateJanusSession()
    {
        var request = new
        {
            janus = "create",
            transaction = Guid.NewGuid().ToString(),
            admin_secret = _janusSettings.AdminSecret
        };
        var response = await _httpClient.PostAsJsonAsync(_janusApiUrl, request);
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JanusResponse>(content);
    }

    private async Task<JanusResponse> AttachVideoRoomPlugin(long sessionId)
    {
        var request = new
        {
            janus = "attach",
            plugin = "janus.plugin.videoroom",
            transaction = Guid.NewGuid().ToString()
        };
        var response = await _httpClient.PostAsJsonAsync($"{_janusApiUrl}/{sessionId}", request);
        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<JanusResponse>(content);
    }

    private int GenerateRoomId() => new Random().Next(1000, 9999);
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