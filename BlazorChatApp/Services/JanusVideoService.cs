using BlazorChatApp.Models;
using BlazorChatApp.Shared.Models.VideoModels;
using BlazorChatApp.Models.Video;
using Microsoft.Extensions.Options;
using Microsoft.JSInterop;
using System.Text.Json;

namespace BlazorChatApp.Services;

public class JanusVideoService : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<JanusVideoService> _logger;
    private readonly JanusSettings _janusSettings;
    private readonly DotNetObjectReference<JanusVideoService> _objRef;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    // Events
    public event Func<string, string, Task>? OnLogMessage;
    public event Func<Task>? OnLocalStreamReady;
    public event Func<VideoParticipant, Task>? OnRemoteStreamAdded;
    public event Func<int, Task>? OnRemoteStreamRemoved;
    public event Func<string, Task>? OnConnectionStateChanged;
    public event Func<string, Task>? OnDeviceChanged;
    public event Func<Task>? OnScreenShareStarted;
    public event Func<Task>? OnScreenShareStopped;

    // State
    private bool _isInitialized = false;
    private bool _isConnected = false;
    private bool _isInRoom = false;
    private bool _isScreenSharing = false;
    private string? _currentRoomId = null;
    private string? _currentDisplayName = null;

    public JanusVideoService(
        IJSRuntime jsRuntime,
        ILogger<JanusVideoService> logger,
        IOptions<JanusSettings> janusOptions)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;
        _janusSettings = janusOptions.Value;
        _objRef = DotNetObjectReference.Create(this);
    }

    #region Public Methods

    public async Task InitializeAsync()
    {
        try
        {
            if (_isInitialized) return;

            _logger.LogInformation("Initializing Janus Video Service...");

            // JS tarafına DotNetObjectReference ve settings gönder
            await _jsRuntime.InvokeVoidAsync("janusInterop.initialize", _objRef, _janusSettings);

            _isInitialized = true;
            _logger.LogInformation("Janus Video Service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Janus Video Service");
            throw;
        }
    }

    public async Task<List<MediaDevice>> GetMediaDevicesAsync()
    {
        try
        {
            _logger.LogDebug("Getting media devices...");
            var devicesJson = await _jsRuntime.InvokeAsync<string>("janusInterop.getMediaDevices");
            var devices = JsonSerializer.Deserialize<List<MediaDevice>>(devicesJson, JsonOptions) ?? new List<MediaDevice>();

            _logger.LogInformation("Found {DeviceCount} media devices", devices.Count);
            return devices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get media devices");
            return new List<MediaDevice>();
        }
    }

    public async Task<List<MediaDevice>> GetAudioInputDevicesAsync()
    {
        var allDevices = await GetMediaDevicesAsync();
        return allDevices.Where(d => d.Kind == "audioinput").ToList();
    }

    public async Task<List<MediaDevice>> GetVideoInputDevicesAsync()
    {
        var allDevices = await GetMediaDevicesAsync();
        return allDevices.Where(d => d.Kind == "videoinput").ToList();
    }

    public async Task<List<MediaDevice>> GetAudioOutputDevicesAsync()
    {
        var allDevices = await GetMediaDevicesAsync();
        return allDevices.Where(d => d.Kind == "audiooutput").ToList();
    }

    public async Task ConnectAsync(string roomId, string displayName)
    {
        try
        {
            if (!_isInitialized)
                throw new InvalidOperationException("Service must be initialized first");

            if (_isConnected)
                throw new InvalidOperationException("Already connected");

            _logger.LogInformation("Connecting to Janus server...");

            await _jsRuntime.InvokeVoidAsync("janusInterop.connect", _janusSettings.WebSocketUrl, roomId, displayName);

            _currentRoomId = roomId;
            _currentDisplayName = displayName;
            _isConnected = true;

            _logger.LogInformation("Connected to Janus server successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to Janus server");
            throw;
        }
    }

    public async Task JoinAndPublishAsync(MediaDeviceConstraints? constraints = null)
    {
        try
        {
            if (!_isConnected)
                throw new InvalidOperationException("Must be connected first");

            if (_isInRoom)
                throw new InvalidOperationException("Already in room");

            _logger.LogInformation("Joining room and starting publish...");

            var constraintsJson = constraints != null
                ? JsonSerializer.Serialize(constraints, JsonOptions)
                : "null";

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            var result = await _jsRuntime.InvokeAsync<string>("janusInterop.joinAndPublish", cts.Token, constraintsJson);

            _isInRoom = true;
            _logger.LogInformation("Joined room and started publishing successfully");
        }
        catch (TaskCanceledException)
        {
            _logger.LogError("Join and publish operation timed out");
            throw new TimeoutException("Join and publish operation timed out after 30 seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to join room and publish");
            throw;
        }
    }
    public async Task SwitchCameraAsync(string deviceId)
    {
        try
        {
            if (!_isInRoom)
                throw new InvalidOperationException("Must be in room to switch camera");

            if (_isScreenSharing)
                throw new InvalidOperationException("Cannot switch camera during screen sharing");

            _logger.LogInformation("Switching camera to device: {DeviceId}", deviceId);

            await _jsRuntime.InvokeVoidAsync("janusInterop.switchCamera", deviceId);

            _logger.LogInformation("Camera switched successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch camera to device: {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task SwitchMicrophoneAsync(string deviceId)
    {
        try
        {
            if (!_isInRoom)
                throw new InvalidOperationException("Must be in room to switch microphone");

            _logger.LogInformation("Switching microphone to device: {DeviceId}", deviceId);

            await _jsRuntime.InvokeVoidAsync("janusInterop.switchMicrophone", deviceId);

            _logger.LogInformation("Microphone switched successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to switch microphone to device: {DeviceId}", deviceId);
            throw;
        }
    }

    public async Task StartScreenShareAsync()
    {
        try
        {
            if (!_isInRoom)
                throw new InvalidOperationException("Must be in room to start screen sharing");

            if (_isScreenSharing)
                throw new InvalidOperationException("Screen sharing already active");

            _logger.LogInformation("Starting screen share...");

            await _jsRuntime.InvokeVoidAsync("janusInterop.startScreenShare");

            _isScreenSharing = true;
            _logger.LogInformation("Screen share started successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start screen share");
            throw;
        }
    }

    public async Task StopScreenShareAsync()
    {
        try
        {
            if (!_isScreenSharing)
                throw new InvalidOperationException("Screen sharing not active");

            _logger.LogInformation("Stopping screen share...");

            await _jsRuntime.InvokeVoidAsync("janusInterop.stopScreenShare");

            _isScreenSharing = false;
            _logger.LogInformation("Screen share stopped successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop screen share");
            throw;
        }
    }

    public async Task LeaveRoomAsync()
    {
        try
        {
            if (!_isInRoom)
                return;

            _logger.LogInformation("Leaving room...");

            await _jsRuntime.InvokeVoidAsync("janusInterop.leaveRoom");

            _isInRoom = false;
            _isScreenSharing = false;
            _logger.LogInformation("Left room successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to leave room");
            throw;
        }
    }

    public async Task DisconnectAsync()
    {
        try
        {
            if (!_isConnected)
                return;

            _logger.LogInformation("Disconnecting from Janus server...");

            if (_isInRoom)
                await LeaveRoomAsync();

            await _jsRuntime.InvokeVoidAsync("janusInterop.disconnect");

            _isConnected = false;
            _currentRoomId = null;
            _currentDisplayName = null;

            _logger.LogInformation("Disconnected from Janus server");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to disconnect from Janus server");
            throw;
        }
    }

    #endregion

    #region JSInvokable Methods (Callbacks from JavaScript)

    [JSInvokable]
    public async Task HandleLogMessage(string message, string level)
    {
        try
        {
            // ILogger ile loglama
            switch (level.ToLower())
            {
                case "err":
                case "error":
                    _logger.LogError("Janus: {Message}", message);
                    break;
                case "warn":
                case "warning":
                    _logger.LogWarning("Janus: {Message}", message);
                    break;
                case "ok":
                case "info":
                    _logger.LogInformation("Janus: {Message}", message);
                    break;
                default:
                    _logger.LogDebug("Janus: {Message}", message);
                    break;
            }

            // Event fırlat
            if (OnLogMessage != null)
                await OnLogMessage.Invoke(message, level);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleLogMessage");
        }
    }

    [JSInvokable]
    public async Task HandleLocalStreamReady()
    {
        try
        {
            _logger.LogInformation("Local stream is ready");
            
            if (OnLocalStreamReady != null)
                await OnLocalStreamReady.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleLocalStreamReady");
        }
    }

    [JSInvokable]
    public async Task HandleRemoteStreamAdded(string participantJson)
    {
        try
        {
            var participant = JsonSerializer.Deserialize<VideoParticipant>(participantJson, JsonOptions);
            if (participant != null)
            {
                _logger.LogInformation("Remote stream added for participant: {UserId}", participant.UserId);
                
                if (OnRemoteStreamAdded != null)
                    await OnRemoteStreamAdded.Invoke(participant);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle remote stream added");
        }
    }

    [JSInvokable]
    public async Task HandleRemoteStreamRemoved(int feedId)
    {
        try
        {
            _logger.LogInformation("Remote stream removed for feed: {FeedId}", feedId);
            
            if (OnRemoteStreamRemoved != null)
                await OnRemoteStreamRemoved.Invoke(feedId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleRemoteStreamRemoved");
        }
    }

    [JSInvokable]
    public async Task HandleConnectionStateChanged(string state)
    {
        try
        {
            _logger.LogInformation("Connection state changed to: {State}", state);
            
            if (OnConnectionStateChanged != null)
                await OnConnectionStateChanged.Invoke(state);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleConnectionStateChanged");
        }
    }

    [JSInvokable]
    public async Task HandleDeviceChanged(string deviceType)
    {
        try
        {
            _logger.LogInformation("Device changed: {DeviceType}", deviceType);
            
            if (OnDeviceChanged != null)
                await OnDeviceChanged.Invoke(deviceType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleDeviceChanged");
        }
    }

    [JSInvokable]
    public async Task HandleScreenShareStarted()
    {
        try
        {
            _isScreenSharing = true;
            _logger.LogInformation("Screen share started");
            
            if (OnScreenShareStarted != null)
                await OnScreenShareStarted.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleScreenShareStarted");
        }
    }

    [JSInvokable]
    public async Task HandleScreenShareStopped()
    {
        try
        {
            _isScreenSharing = false;
            _logger.LogInformation("Screen share stopped");
            
            if (OnScreenShareStopped != null)
                await OnScreenShareStopped.Invoke();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in HandleScreenShareStopped");
        }
    }

    #endregion

    #region Properties

    public bool IsInitialized => _isInitialized;
    public bool IsConnected => _isConnected;
    public bool IsInRoom => _isInRoom;
    public bool IsScreenSharing => _isScreenSharing;
    public string? CurrentRoomId => _currentRoomId;
    public string? CurrentDisplayName => _currentDisplayName;

    #endregion

    #region IAsyncDisposable

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_isConnected)
                await DisconnectAsync();

            _objRef?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during disposal");
        }
    }

    #endregion
}