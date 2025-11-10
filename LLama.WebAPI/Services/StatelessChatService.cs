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

            var sec = configuration.GetSection("LLama");
            var modelPaths = sec.GetSection("ModelPath").Get<string[]>() ?? Array.Empty<string>();
            var modelPath = modelPaths.FirstOrDefault();

            if (string.IsNullOrWhiteSpace(modelPath))
            {
                throw new InvalidOperationException(" No model path found in appsettings.Development.json.");
            }

            var ctxSize = sec.GetValue<uint?>("ContextSize") ?? 512;
            var gpuLayers = sec.GetValue<int?>("GpuLayerCount") ?? 0;

            // Load weights only once (thread-safe)
            if (_sharedWeights == null)
            {
                lock (typeof(StatelessChatService))
                {
                    if (_sharedWeights == null)
                    {
                        var @params = new ModelParams(modelPath)
                        {
                            ContextSize = ctxSize,
                            GpuLayerCount = gpuLayers,
                        };
                        _sharedWeights = LLamaWeights.LoadFromFile(@params);
                        log.LogInformation(" Shared LLama weights loaded from {ModelPath}", modelPath);
                    }
                }
            }

            //  Create lightweight context for this instance
            var contextParams = new ModelParams(modelPath)
            {
                ContextSize = ctxSize,
                GpuLayerCount = gpuLayers
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
