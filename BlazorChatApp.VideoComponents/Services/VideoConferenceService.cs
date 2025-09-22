using System;
using System.Collections.Generic;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using BlazorChatApp.Models;
using BlazorChatApp.Models.Video;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;

namespace BlazorChatApp.Services;

public class VideoConferenceService : IVideoConferenceService, IDisposable
{
    private volatile bool _connected;
    private readonly JanusSettings _settings;
    private readonly IJSRuntime _jsRuntime;
    private ClientWebSocket _webSocket;
    private long _sessionId;
    private long _handleId; // VideoRoom plugin handle
    private readonly Dictionary<string, TaskCompletionSource<JsonElement>> _pendingTransactions = new();
    private readonly Dictionary<string, Action<JsonElement>> _eventHandlers = new(); // Custom event handling
    private CancellationTokenSource _cts = new();
    private readonly Dictionary<string, VideoRoomInfo> _cachedRooms = new(); // Simple cache for rooms

    public VideoConferenceService(IOptions<JanusSettings> settings, IJSRuntime jsRuntime)
    {
        _settings = settings.Value;
        _jsRuntime = jsRuntime;
       
    }

    public async Task InitializeAsync()
    {
        if (_connected) return;

        _webSocket = new ClientWebSocket();
        _webSocket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(8));
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token, timeoutCts.Token);

        await _webSocket.ConnectAsync(new Uri(_settings.WebSocketUrl), linked.Token);

        _ = ReceiveMessagesAsync();
        _ = SendKeepAliveAsync();

        // Create session
        var transaction = Guid.NewGuid().ToString();
        var createSessionMsg = new { janus = "create", transaction };
        await SendMessageAsync(createSessionMsg);
        var response = await WaitForTransactionAsync(transaction);
        _sessionId = response.GetProperty("data").GetProperty("id").GetInt64();

        // Attach to VideoRoom plugin
        transaction = Guid.NewGuid().ToString();
        var attachMsg = new { janus = "attach", session_id = _sessionId, plugin = "janus.plugin.videoroom", transaction };
        await SendMessageAsync(attachMsg);
        response = await WaitForTransactionAsync(transaction);
        _handleId = response.GetProperty("data").GetProperty("id").GetInt64();
        _connected = true;
    }

    private async Task SendMessageAsync(object message)
    {
        try
        {
            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, _cts.Token);
        }
        catch (Exception ex)
        {
            // Handle send error, reconnect if needed
            Console.WriteLine($"Send error: {ex.Message}");
        }
    }

    private async Task ReceiveMessagesAsync()
    {
        var buffer = new byte[8192];
        while (!_cts.IsCancellationRequested)
        {
            try
            {
                var result = await _webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    var doc = JsonDocument.Parse(json);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("transaction", out var txProp))
                    {
                        var tx = txProp.GetString();
                        if (_pendingTransactions.TryGetValue(tx, out var tcs))
                        {
                            tcs.SetResult(root);
                            _pendingTransactions.Remove(tx);
                        }
                    }
                    else if (root.GetProperty("janus").GetString() == "event")
                    {
                        // Handle plugin events
                        var sender = root.TryGetProperty("sender", out var senderProp) ? senderProp.GetInt64() : 0;
                        var pluginData = root.GetProperty("plugindata").GetProperty("data");
                        var videoroomEvent = pluginData.GetProperty("videoroom").GetString();

                        // Example events: joined, published, leaving, destroyed
                        if (videoroomEvent == "joined")
                        {
                            // Update participants cache, invoke JS for WebRTC if needed
                        }
                        else if (videoroomEvent == "destroyed")
                        {
                            // Remove from cache
                        }
                        // JSEP handling
                        if (root.TryGetProperty("jsep", out var jsep))
                        {
                            await HandleJsepAsync(jsep);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Receive error: {ex.Message}");
                // Reconnect logic here
            }
        }
    }

    private async Task HandleJsepAsync(JsonElement jsep)
    {
        var type = jsep.GetProperty("type").GetString();
        var sdp = jsep.GetProperty("sdp").GetString();
        if (type == "offer" || type == "answer")
        {
            // Pass to JS for PeerConnection
            await _jsRuntime.InvokeVoidAsync("handleRemoteDescription", type, sdp);
        }
    }

    private Task<JsonElement> WaitForTransactionAsync(string transaction, int timeoutMs = 5000)
    {
        var tcs = new TaskCompletionSource<JsonElement>();
        _pendingTransactions[transaction] = tcs;
        // Timeout logic
        _ = Task.Delay(timeoutMs).ContinueWith(_ =>
        {
            if (_pendingTransactions.Remove(transaction, out var pendingTcs))
                pendingTcs.SetException(new TimeoutException("Transaction timeout"));
        });
        return tcs.Task;
    }

    private async Task SendKeepAliveAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            await Task.Delay(30000, _cts.Token);
            var transaction = Guid.NewGuid().ToString();
            var keepAlive = new { janus = "keepalive", session_id = _sessionId, transaction };
            await SendMessageAsync(keepAlive);
            // Ignore response
        }
    }

    private long GetJanusRoomId(string roomId) => long.Parse(roomId); // Assume roomId is string representation of long

    private long GetJanusUserId(string userId) => Math.Abs(userId.GetHashCode() % long.MaxValue); // Simple mapping

    public async Task<VideoRoomInfo> CreateRoomAsync(string roomName, int maxParticipants, string createdByUserId)
    {
        var transaction = Guid.NewGuid().ToString();
        var roomIdLong = DateTime.UtcNow.Ticks % 1000000000; // Unique, production'da random big int
        var body = new
        {
            request = "create",
            room = roomIdLong,
            description = roomName,
            publishers = maxParticipants,
            admin_key = _settings.AdminSecret,
            is_private = false // Default
        };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "created")
            throw new Exception(pluginData.GetProperty("error").GetString());

        var roomId = roomIdLong.ToString();
        var roomInfo = new VideoRoomInfo
        {
            RoomId = roomId,
            RoomName = roomName,
            MaxParticipants = maxParticipants,
            CreatedAt = DateTime.UtcNow,
            ApiUrl = _settings.ApiUrl,
            WebSocketUrl = _settings.WebSocketUrl,
            Status = VideoRoomStatus.Active,
            CurrentParticipantCount = 0
        };
        _cachedRooms[roomId] = roomInfo;
        return roomInfo;
    }

    public async Task<VideoRoomInfo> JoinRoomAsync(string roomId, string userId, string displayName)
    {
        var transaction = Guid.NewGuid().ToString();
        var roomIdLong = GetJanusRoomId(roomId);
        var userIdLong = GetJanusUserId(userId);
        var body = new
        {
            request = "join",
            ptype = "publisher",
            room = roomIdLong,
            id = userIdLong,
            display = displayName
        };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "joined")
            throw new Exception(pluginData.GetProperty("error").GetString());

        // Get local media and publish
        await _jsRuntime.InvokeVoidAsync("getUserMediaAndPublish", roomId, userId, displayName);

        var roomInfo = await GetRoomInfoAsync(roomId);
        roomInfo.CurrentParticipantCount++;
        // Update participant
        roomInfo.ConnectionParams[userId] = new { HandleId = _handleId }; // Example
        return roomInfo;
    }

    public async Task LeaveRoomAsync(string roomId, string userId)
    {
        var transaction = Guid.NewGuid().ToString();
        var body = new { request = "leave" };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "event" || pluginData.GetProperty("leaving").GetString() != "ok")
            throw new Exception("Leave failed");

        // Clean JS
        await _jsRuntime.InvokeVoidAsync("cleanupPeerConnection", userId);
    }

    public async Task<List<VideoParticipant>> GetRoomParticipantsAsync(string roomId)
    {
        var transaction = Guid.NewGuid().ToString();
        var roomIdLong = GetJanusRoomId(roomId);
        var body = new { request = "listparticipants", room = roomIdLong };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "participants")
            throw new Exception(pluginData.GetProperty("error").GetString());

        var participantsArray = pluginData.GetProperty("participants").EnumerateArray();
        var list = new List<VideoParticipant>();
        foreach (var p in participantsArray)
        {
            list.Add(new VideoParticipant
            {
                UserId = p.GetProperty("id").GetInt64().ToString(),
                DisplayName = p.GetProperty("display").GetString(),
                IsMuted = !p.GetProperty("audio_active").GetBoolean(),
                IsVideoEnabled = p.GetProperty("video_active").GetBoolean(),
                JoinedAt = DateTime.UtcNow, // Approx
                Role = VideoParticipantRole.Participant // Default, customize if needed
            });
        }
        return list;
    }

    public async Task<VideoRoomInfo?> GetRoomInfoAsync(string roomId)
    {
        var rooms = await GetActiveRoomsAsync();
        return rooms.Find(r => r.RoomId == roomId);
    }

    public async Task<List<VideoRoomInfo>> GetActiveRoomsAsync()
    {
        var transaction = Guid.NewGuid().ToString();
        var body = new { request = "list" };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "success")
            throw new Exception(pluginData.GetProperty("error").GetString());

        var roomsArray = pluginData.GetProperty("rooms").EnumerateArray();
        var list = new List<VideoRoomInfo>();
        foreach (var room in roomsArray)
        {
            var roomId = room.GetProperty("room").GetInt64().ToString();
            list.Add(new VideoRoomInfo
            {
                RoomId = roomId,
                RoomName = room.GetProperty("description").GetString(),
                MaxParticipants = room.GetProperty("max_publishers").GetInt32(),
                CurrentParticipantCount = room.GetProperty("num_participants").GetInt32(),
                CreatedAt = DateTime.UtcNow, // Not provided, approx
                Status = VideoRoomStatus.Active
            });
        }
        return list;
    }

    public async Task SendVideoSignalAsync(string roomId, VideoSignalData signalData)
    {
        // Assuming signalData.Type is "offer", "answer", "candidate"
        var transaction = Guid.NewGuid().ToString();
        var jsep = new { type = signalData.Type, sdp = signalData.Data.ToString() }; // Example for SDP
        var body = new { request = "configure" }; // Or "start" for subscriber
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body, jsep };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);
        // Handle response
    }

    public async Task DeleteRoomAsync(string roomId, string requestedByUserId)
    {
        var transaction = Guid.NewGuid().ToString();
        var roomIdLong = GetJanusRoomId(roomId);
        var body = new
        {
            request = "destroy",
            room = roomIdLong,
            admin_key = _settings.AdminSecret // Assume admin
        };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "destroyed")
            throw new Exception(pluginData.GetProperty("error").GetString());

        _cachedRooms.Remove(roomId);
    }

    public async Task ToggleParticipantMediaAsync(string roomId, string userId, bool isMuted, bool isVideoEnabled)
    {
        var transaction = Guid.NewGuid().ToString();
        var body = new
        {
            request = "configure",
            audio = !isMuted,
            video = isVideoEnabled
        };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);
        // Check configured
    }

    public async Task UpdateParticipantRoleAsync(string roomId, string userId, VideoParticipantRole role, string requestedByUserId)
    {
        // Janus'ta direct role yok, assume custom via allowed or edit
        // For simplicity, skip or use custom event
        throw new NotImplementedException("Role update not directly supported, customize with allowed array");
    }

    public async Task KickParticipantAsync(string roomId, string userId, string requestedByUserId)
    {
        var transaction = Guid.NewGuid().ToString();
        var roomIdLong = GetJanusRoomId(roomId);
        var userIdLong = GetJanusUserId(userId);
        var body = new
        {
            request = "kick",
            room = roomIdLong,
            id = userIdLong,
            admin_key = _settings.AdminSecret
        };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        if (pluginData.GetProperty("videoroom").GetString() != "success")
            throw new Exception(pluginData.GetProperty("error").GetString());
    }

    public async Task<bool> IsRoomExistsAsync(string roomId)
    {
        var transaction = Guid.NewGuid().ToString();
        var roomIdLong = GetJanusRoomId(roomId);
        var body = new { request = "exists", room = roomIdLong };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body };
        await SendMessageAsync(msg);
        var response = await WaitForTransactionAsync(transaction);

        var pluginData = response.GetProperty("plugindata").GetProperty("data");
        return pluginData.GetProperty("exists").GetBoolean();
    }

    public async Task<bool> IsUserInRoomAsync(string roomId, string userId)
    {
        var participants = await GetRoomParticipantsAsync(roomId);
        return participants.Exists(p => p.UserId == userId);
    }

    [JSInvokable]
    public async Task SendOfferToJanus(string sdp)
    {
        // Called from JS, send to Janus
        var transaction = Guid.NewGuid().ToString();
        var jsep = new { type = "offer", sdp };
        var body = new { request = "publish" };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body, jsep };
        await SendMessageAsync(msg);
    }

    [JSInvokable]
    public async Task SendAnswerToJanus(string sdp)
    {
        // Similar for answer
        var transaction = Guid.NewGuid().ToString();
        var jsep = new { type = "answer", sdp };
        var body = new { request = "start" };
        var msg = new { janus = "message", session_id = _sessionId, handle_id = _handleId, transaction, body, jsep };
        await SendMessageAsync(msg);
    }

    [JSInvokable]
    public async Task HandleIceCandidate(string candidateJson)
    {
        var transaction = Guid.NewGuid().ToString();
        var candidate = JsonNode.Parse(candidateJson);
        var jsep = new { candidate = candidate["candidate"], sdpMid = candidate["sdpMid"], sdpMLineIndex = candidate["sdpMLineIndex"] };
        var body = new { request = "trickle" };
        var msg = new { janus = "trickle", session_id = _sessionId, handle_id = _handleId, transaction, candidate = jsep };
        await SendMessageAsync(msg);
    }

    public void Dispose()
    {
        try
        {
        _cts.Cancel();
        _webSocket?.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposed", CancellationToken.None).Wait();
        _webSocket?.Dispose();

        }
        catch (Exception e)
        { 
        }
    }
}