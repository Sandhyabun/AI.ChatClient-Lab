using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LLama.Web.Services;

public sealed class SignalRBootstrapper(StreamService stream, ILogger<SignalRBootstrapper> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await stream.StartAsync(token =>
        {
            // Log each token emitted by the stream 
            logger.LogInformation("Received stream token: {Token}", token);
        });

        logger.LogInformation("StreamService started successfully.");
    }

    public async Task StopAsync(CancellationToken ct)
    {
        await stream.StopAsync(ct);
        logger.LogInformation("StreamService connection stopped and disposed.");
    }
}