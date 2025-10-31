using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using LLama.Web.Hubs;
using System.Threading;

namespace LLama.Web.Services
{
    public sealed class StreamService
    {
        private readonly HubConnection _connection;
        private readonly SemaphoreSlim _lock = new(1, 1);

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
            await _lock.WaitAsync();
            try
            {
                _connection.On<string>("ReceiveToken", onToken);
                _connection.On("StreamComplete", () =>
                    Console.WriteLine("Stream finished"));

                if (_connection.State == HubConnectionState.Disconnected)
                {
                    await _connection.StartAsync();
                    Console.WriteLine("Connected to LlamaHub");

                    // Optional: check connectivity
                    await _connection.InvokeAsync("PingServer", "Hello backend!");
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task SendPrompt(string prompt)
        {
            await _lock.WaitAsync();
            try
            {
                await _connection.InvokeAsync("StreamResponse", prompt);
            }
            finally
            {
                _lock.Release();
            }
        }

        public async Task StopAsync(CancellationToken ct)
        {
            await _lock.WaitAsync(ct);
            try
            {
                await _connection.StopAsync(ct);
                await _connection.DisposeAsync();
                Console.WriteLine("StreamService connection stopped and disposed");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error stopping StreamService: {ex.Message}");
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
