using System.Text.Json.Serialization;

namespace BlazorChatApp.Shared.Models.VideoModels;



public class MediaDevice
{
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = "";
    
    [JsonPropertyName("label")]
    public string Label { get; set; } = "";
    
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = "";
}