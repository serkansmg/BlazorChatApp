using System.Text.Json.Serialization;

namespace BlazorChatApp.Models;

public class UploadResponse
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = "";
    
    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = "";
    
    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }
    
    [JsonPropertyName("mimeType")]
    public string MimeType { get; set; } = "";
    
    [JsonPropertyName("messageType")]
    public string MessageType { get; set; } = "";
}