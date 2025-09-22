namespace BlazorChatApp.Models;

public class JanusSettings
{
    public string ApiUrl { get; set; } = "";
    public string WebSocketUrl { get; set; } = "";
    public string AdminSecret { get; set; } = "";
    public string ServerName { get; set; } = "";
    public int SessionTimeout { get; set; } = 60;
    public int ReclaimSessionTimeout { get; set; } = 0;
    public JanusDebugSettings Debug { get; set; } = new();
}

public class JanusDebugSettings
{
    public int Level { get; set; } = 4;
    public bool Timestamps { get; set; } = true;
    public bool Colors { get; set; } = true;
}