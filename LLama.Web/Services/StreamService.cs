using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using LLama.Web.Hubs;

namespace LLama.Web.Services;

public sealed class StreamService
{
    private readonly HubConnection _connection;
    
    public StreamService(IConfiguration configuration)
    {
        var hubUrl = configuration["LLama:SignalRHub"];
        ArgumentException.ThrowIfNullOrWhiteSpace(hubUrl);
        

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUrl)
            .WithAutomaticReconnect()
            .Build();
    }


    public async Task StartAsync(Action<string> onToken)
    {
        _connection.On<string>("ReceiveToken", onToken);
        _connection.On("StreamComplete", () => Console.WriteLine(" Stream finished"));

        if (_connection.State == HubConnectionState.Disconnected)
            await _connection.StartAsync();

        Console.WriteLine(" Connected to LlamaHub");

        await _connection.InvokeAsync("PingServer", "Hello backend!");
    }


    public Task SendPrompt(string prompt) =>
        _connection.InvokeAsync("StreamResponse", prompt);
    public async Task StopAsync(CancellationToken ct)
    {
        if (_connection != null)
        {
            await _connection.StopAsync(ct);
            await _connection.DisposeAsync();
            Console.WriteLine(" StreamService connection stopped and disposed");
        }
    }
}