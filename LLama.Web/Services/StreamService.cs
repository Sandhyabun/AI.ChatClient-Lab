using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using System.Threading;

namespace LLama.Web.Services
{
    public sealed class StreamService
    {
        private readonly HubConnection _connection;
        private readonly SemaphoreSlim _lock = new(1, 1);
        private bool _handlersRegistered;

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
                // Register handlers once (avoid duplicates on reconnect)
                if (!_handlersRegistered)
                {
                    _connection.Remove("ReceiveToken");
                    _connection.Remove("StreamComplete");

                    _connection.On<string>("ReceiveToken", onToken);
                    _connection.On("StreamComplete", () =>
                        Console.WriteLine("Stream finished"));

                    _handlersRegistered = true;
                }

                // Connect with small retry loop
                if (_connection.State == HubConnectionState.Disconnected)
                {
                    for (var attempt = 1; attempt <= 5; attempt++)
                    {
                        try
                        {
                            await _connection.StartAsync();
                            Console.WriteLine("✅ Connected to Llamahub");
                            break;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"⚠️ Connect attempt {attempt} failed: {ex.Message}");
                            if (attempt == 5) throw; // bubble last failure
                            await Task.Delay(1500);
                        }
                    }

                    // Optional: health check to verify server method exists
                    try
                    {
                        await _connection.InvokeAsync("PingServer", "Hello backend!");
                    }
                    catch (Exception ex)
                    {
                        // Don’t kill the process if the hub doesn’t implement PingServer
                        Console.WriteLine($"PingServer failed: {ex.Message}");
                    }
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
                // If we got disconnected, try to reconnect quickly
                if (_connection.State == HubConnectionState.Disconnected)
                {
                    try { await _connection.StartAsync(); }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Reconnect failed: {ex.Message}");
                        throw;
                    }
                }

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
                if (_connection.State != HubConnectionState.Disconnected)
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
