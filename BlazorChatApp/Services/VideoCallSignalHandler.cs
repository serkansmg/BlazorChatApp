using System.Text.Json;
using Microsoft.JSInterop;
using BlazorChatApp.Services;
using BlazorChatApp.Shared.Models.VideoModels;

namespace BlazorChatApp.Services;

public class VideoCallSignalHandler : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private readonly EventBus _eventBus;
    private DotNetObjectReference<VideoCallSignalHandler>? _dotNetRef;

    public VideoCallSignalHandler(IJSRuntime jsRuntime, EventBus eventBus)
    {
        _jsRuntime = jsRuntime;
        _eventBus = eventBus;
    }

    public async Task InitializeAsync()
    {
        _dotNetRef = DotNetObjectReference.Create(this);
        await _jsRuntime.InvokeVoidAsync("setVideoCallHandler", _dotNetRef);
    }

    [JSInvokable]
    public async Task OnVideoSignalGenerated(string signalType, string data, string targetUserId)
    {
        Console.WriteLine($"Video signal generated: {signalType} for user {targetUserId}");
        
        // EventBus üzerinden video signal'i yayınla
        _eventBus.PublishVideoCallSignal(new
        {
            Type = signalType,
            Data = data,
            TargetUserId = targetUserId,
            Timestamp = DateTime.UtcNow
        });
    }

    [JSInvokable]
    public void OnVideoCallStateChanged(string state)
    {
        Console.WriteLine($"Video call state changed: {state}");
        
        _eventBus.PublishVideoCallStateChange(state);
    }
    
    [JSInvokable]
    public void OnCallAccepted()
    {
        Console.WriteLine("OnCallAccepted called from JS");
        // EventBus ile VideoCall bileşenine bildir
    }

    [JSInvokable]
    public void OnMediaDevicesLoaded(string videoDevicesJson, string audioDevicesJson)
    {
        try
        {
            Console.WriteLine($"Media devices received: video={videoDevicesJson}, audio={audioDevicesJson}");
        
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
        
            var videoDevices = JsonSerializer.Deserialize<List<MediaDevice>>(videoDevicesJson, options) ?? new();
            var audioDevices = JsonSerializer.Deserialize<List<MediaDevice>>(audioDevicesJson, options) ?? new();
        
            Console.WriteLine($"Parsed: {videoDevices.Count} video devices, {audioDevices.Count} audio devices");
        
            _eventBus.PublishMediaDevicesLoaded(videoDevices, audioDevices);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error parsing media devices: {ex.Message}");
            Console.WriteLine($"StackTrace: {ex.StackTrace}");
        }
    }
    
    [JSInvokable]
    public void OnVideoCallError(string error)
    {
        Console.WriteLine($"Video call error: {error}");
        
        _eventBus.PublishVideoCallError(error);
    }

    public async ValueTask DisposeAsync()
    {
        _dotNetRef?.Dispose();
    }
}