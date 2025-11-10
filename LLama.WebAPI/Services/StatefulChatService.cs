using LLama.WebAPI.Models;
using LLama.Sampling;
using LLama.Common;
using Microsoft.Extensions.Configuration;

namespace LLama.WebAPI.Services;

public sealed class StatefulChatService
    : IDisposable
{
    private readonly ChatSession _session;
    private readonly LLamaContext _context;
    private readonly ILogger<StatefulChatService> _logger;
    private bool _continue = false;
    private readonly LLamaWeights _weights;

    private const string SystemPrompt = "Transcript of a dialog, where the User interacts with an Assistant. Assistant is helpful, kind, honest, good at writing, and never fails to answer the User's requests immediately and with precision.";

    public StatefulChatService(IConfiguration configuration, ILogger<StatefulChatService> logger)
    {
        _logger = logger;
        var sec = configuration.GetSection("LLama");

        //  Read all model paths from config (array)
        var firstModel = sec.GetSection("Models").GetChildren().FirstOrDefault();
        var modelPath  = firstModel?.GetValue<string>("ModelPath");
      

        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new InvalidOperationException(" No model path found in appsettings.Development.json.");
        }

        //  Context & GPU settings
        var ctxSize = sec.GetValue<uint?>("ContextSize") ?? 512;
        var gpuLayers = sec.GetValue<int?>("GpuLayerCount") ?? 0;

        var @params = new ModelParams(modelPath)
        {
            ContextSize = ctxSize,
            GpuLayerCount = gpuLayers
        };

        //  Load weights 
        _weights = LLamaWeights.LoadFromFile(@params);
        _context = _weights.CreateContext(@params);

        //   Create chat session
        _session = new ChatSession(new InteractiveExecutor(_context));
        _session.History.AddMessage(Common.AuthorRole.System, SystemPrompt);

        _logger.LogInformation(" Loaded default model: {model}", Path.GetFileName(modelPath));
    }


    public void Dispose()
    {
        _context?.Dispose();
    }

    public async Task<string> Send(SendMessageInput input)
    {

        if (!_continue)
        {
            _logger.LogInformation("Prompt: {text}", SystemPrompt);
            _continue = true;
        }
        _logger.LogInformation("Input: {text}", input.Text);
        var outputs = _session.ChatAsync(
            new Common.ChatHistory.Message(Common.AuthorRole.User, input.Text),
            new Common.InferenceParams
            {
                AntiPrompts = ["User:"],

                SamplingPipeline = new DefaultSamplingPipeline
                {
                    RepeatPenalty = 1.0f
                }
            });

        var result = "";
        await foreach (var output in outputs)
        {
            _logger.LogInformation("Message: {output}", output);
            result += output;
        }

        return result;
    }

    public async IAsyncEnumerable<string> SendStream(SendMessageInput input)
    {
        if (!_continue)
        {
            _logger.LogInformation(SystemPrompt);
            _continue = true;
        }

        _logger.LogInformation(input.Text);

        var outputs = _session.ChatAsync(
            new Common.ChatHistory.Message(Common.AuthorRole.User, input.Text),
            new Common.InferenceParams
            {
                AntiPrompts = ["User:"],

                SamplingPipeline = new DefaultSamplingPipeline
                {
                    RepeatPenalty = 1.0f
                }
            });

        await foreach (var output in outputs)
        {
            _logger.LogInformation(output);
            yield return output;
        }
    }
}