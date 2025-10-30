using LLama.Common;
using System.Text;
using static LLama.LLamaTransforms;
using Microsoft.Extensions.Logging;
namespace LLama.WebAPI.Services
{
    public class StatelessChatService
    {
        private readonly LLamaContext _context;
        private readonly LLamaWeights _weights;
        private readonly ILogger<StatelessChatService> _log;

        public StatelessChatService(IConfiguration configuration, ILogger<StatelessChatService> log)
        {
            _log = log;
            var sec = configuration.GetSection("LLama");
            var modelPath = sec.GetValue<string>("ModelPath")!;
            var ctxSize = sec.GetValue<uint?>("ContextSize") ?? 512;
            var gpuLayers = sec.GetValue<int?>("GpuLayerCount") ?? 0;
            var @params = new ModelParams(modelPath)
            {
                ContextSize = ctxSize,
                GpuLayerCount = gpuLayers,
            };
            // todo: share weights from a central service
            _weights = LLamaWeights.LoadFromFile(@params);
            _context = new LLamaContext(_weights, @params);



        }

        public async Task<string> SendAsync(ChatHistory history)
        {
            ArgumentNullException.ThrowIfNull(history);

            var last = history.Messages?.LastOrDefault();
            ArgumentException.ThrowIfNullOrWhiteSpace(last?.Content);
            try
            {
                // existing LLama pipeline 
                var session =
                    new ChatSession(new InteractiveExecutor(_context))
                        .WithOutputTransform(new LLamaTransforms.KeywordTextOutputStreamTransform(
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
    }
    public class HistoryTransform : DefaultHistoryTransform
    {
        public override string HistoryToText(ChatHistory history)
        {
            return base.HistoryToText(history) + "\n Assistant:";
        }

    }
}