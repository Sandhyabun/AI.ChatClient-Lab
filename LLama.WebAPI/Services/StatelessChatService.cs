using LLama.Common;
using System.Text;
using static LLama.LLamaTransforms;
using Microsoft.Extensions.Logging;
namespace LLama.WebAPI.Services
{
    public class StatelessChatService : IDisposable
    { 
        private static LLamaWeights? _sharedWeights; // Shared across instances
        private readonly LLamaContext _context;
        private readonly ILogger<StatelessChatService> _log;
        private bool _disposed;
        public StatelessChatService(IConfiguration configuration, ILogger<StatelessChatService> log)
        {
            _log = log;

            // Load weights only once 
            if (_sharedWeights == null)
            {
                lock (typeof(StatelessChatService))
                {
                    if (_sharedWeights == null)
                    {
                        var sec = configuration.GetSection("LLama");
                        var modelPath = sec.GetValue<string>("ModelPath")!;
                        var ctxSize = sec.GetValue<uint?>("ContextSize") ?? 512;
                        var gpuLayers = sec.GetValue<int?>("GpuLayerCount") ?? 0;

                        var @params = new ModelParams(modelPath)
                        {
                            ContextSize = ctxSize,
                            GpuLayerCount = gpuLayers,
                        };

                        _sharedWeights = LLamaWeights.LoadFromFile(@params);
                        log.LogInformation("✅ Shared LLama weights loaded from {ModelPath}", modelPath);
                    }
                }
            }

            // Create a new lightweight context for each service instance
            var contextParams = new ModelParams(configuration.GetValue<string>("LLama:ModelPath")!)
            {
                ContextSize = configuration.GetValue<uint?>("LLama:ContextSize") ?? 512,
                GpuLayerCount = configuration.GetValue<int?>("LLama:GpuLayerCount") ?? 0
            };
            _context = new LLamaContext(_sharedWeights!, contextParams);
        }

        public async Task<string> SendAsync(ChatHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);

            var last = history.Messages?.LastOrDefault();
            ArgumentException.ThrowIfNullOrWhiteSpace(last?.Content);
            try
            {
                var session = new ChatSession(new InteractiveExecutor(_context))
                    .WithOutputTransform(new KeywordTextOutputStreamTransform(
                        keywords: new[] { "User:", "Assistant:" }, redundancyLength: 8))
                    .WithHistoryTransform(new HistoryTransform());

                var resultStream = session.ChatAsync(
                    history,
                    new InferenceParams { AntiPrompts = new[] { "User:" } }
                );

                var sb = new StringBuilder();
                await foreach (var chunk in resultStream)
                {
                    _log.LogDebug("{Chunk}", chunk);
                    sb.Append(chunk);
                }

                return sb.ToString();
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "MCP stateless send failed.");
                throw;
            }
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _log.LogInformation("🧹 Disposing StatelessChatService context...");
            _context?.Dispose();
            _disposed = true;
        }
    }

    public class HistoryTransform : DefaultHistoryTransform
    {
        public override string HistoryToText(ChatHistory history)
        {
            return base.HistoryToText(history) + "\n Assistant:";
        }
    }
}
