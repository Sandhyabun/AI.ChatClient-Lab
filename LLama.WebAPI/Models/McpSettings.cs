namespace LLama.WebAPI.Models;


public class McpSettings
{
    public string ServerUrl { get; set; } = string.Empty;
    public string? ApiKey { get; set; }
    public bool EnableStreaming { get; set; }
}