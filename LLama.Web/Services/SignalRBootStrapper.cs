using Microsoft.Extensions.Hosting;

namespace LLama.Web.Services;

public sealed class SignalRBootstrapper(StreamService stream) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
        => await stream.StartAsync(token => Console.Write(token));

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}