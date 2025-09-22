namespace BlazorChatApp.Shared.Models.VideoModels;
 
public class MediaDeviceConstraints
{
    public VideoConstraints? Video { get; set; }
    public AudioConstraints? Audio { get; set; }
}

public class VideoConstraints
{
    public string? DeviceId { get; set; }
    public bool? Width { get; set; }
    public bool? Height { get; set; }
    public bool? FrameRate { get; set; }
}

public class AudioConstraints
{
    public string? DeviceId { get; set; }
    public bool? SampleRate { get; set; }
    public bool? ChannelCount { get; set; }
    public bool? EchoCancellation { get; set; }
}