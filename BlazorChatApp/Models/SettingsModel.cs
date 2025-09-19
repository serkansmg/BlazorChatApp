namespace BlazorChatApp.Models;

public class SettingsModel
{
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    
    // Kestrel ayarları da burada olacak sanırım
    public int DefaultHttpListenPort { get; set; } = 5000;
    public int MaxConcurrentConnections { get; set; } = 100;
    public long MaxRequestBodySize { get; set; } = 30_000_000; // 30MB
}