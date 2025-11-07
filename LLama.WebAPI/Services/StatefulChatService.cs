using System.Collections.Generic;
using System.Linq;
using System.Text;
using LLama.Common;
using LLama.Sampling;
using LLama.WebAPI.Models;
using Microsoft.Extensions.Logging;

namespace LLama.WebAPI.Services;

public sealed class StatefulChatService : IDisposable
{
    private readonly IDecodingDefaultsProvider _decoding;
    private readonly IChatFormatterProvider _formatters;    // <-- add
    private readonly ModelManager _models;
    private readonly ILogger<StatefulChatService> _log;

    private string _boundModel = string.Empty;
    private readonly List<(string role, string content)> _turns = new(); // minimal in-memory history
    private bool _initialized;

    private const string SystemPrompt =
        "Transcript of a dialog, where the User interacts with an Assistant. " +
        "Assistant is helpful, kind, honest, good at writing, and answers precisely.";

    public StatefulChatService(
        ModelManager models,
        IDecodingDefaultsProvider decoding,
        IChatFormatterProvider formatters,              
        ILogger<StatefulChatService> log)
    {
        _models = models;
        _decoding = decoding;
        _formatters = formatters;
        _log = log;

        // bind to the startup model
        _boundModel = _models.CurrentModelName ?? string.Empty;
        _turns.Clear();
    }

    public void Dispose()
    {
        // ModelManager owns contexts. Nothing to dispose here.
    }

    private void EnsureBound()
    {
        var cur = _models.CurrentModelName ?? string.Empty;
        if (!string.Equals(_boundModel, cur, System.StringComparison.Ordinal))
        {
            _log.LogInformation("Rebinding chat to model: {Model}", cur);
            _boundModel = cur;
            _turns.Clear();           // new model = fresh dialogue (optional)
            _initialized = false;
        }
    }

    public async Task<string> Send(SendMessageInput input)
    {
        EnsureBound();

        if (!_initialized)
        {
            _log.LogInformation("System: {SystemPrompt}", SystemPrompt);
            _initialized = true;
        }

        _turns.Add(("user", input.Text));
        _log.LogInformation("User: {Text}", input.Text);

        // 1) Render prompt with model-specific tags
        var fmt = _formatters.For(_boundModel);
        var prompt = fmt.Render(SystemPrompt, _turns, nextRole: "assistant");

        // 2) Get decoding defaults (stops/penalties/maxtokens)
        var infer = _decoding.For(_boundModel);
        LogDecoding(infer);

        // 3) Acquire a context lease so it can't be disposed mid-stream
        using var lease = _models.Acquire(_boundModel);
        var session = new ChatSession(new InteractiveExecutor(lease.Context));

        // 4) Generate using the string overload
        var stream = session.ChatAsync(prompt, infer);

        var sb = new StringBuilder();
        await foreach (var chunk in stream)
        {
            _log.LogDebug("Chunk: {Chunk}", chunk);
            sb.Append(chunk);
        }

        var answer = sb.ToString();
        _turns.Add(("assistant", answer));  // keep minimal history
        return answer;
    }

    public async IAsyncEnumerable<string> SendStream(SendMessageInput input)
    {
        EnsureBound();

        if (!_initialized)
        {
            _log.LogInformation("System: {SystemPrompt}", SystemPrompt);
            _initialized = true;
        }

        _turns.Add(("user", input.Text));
        _log.LogInformation("User: {Text}", input.Text);

        var fmt = _formatters.For(_boundModel);
        var prompt = fmt.Render(SystemPrompt, _turns, nextRole: "assistant");

        var infer = _decoding.For(_boundModel);
        LogDecoding(infer);

        using var lease = _models.Acquire(_boundModel);
        var session = new ChatSession(new InteractiveExecutor(lease.Context));
        var stream = session.ChatAsync(prompt, infer);

        var sb = new StringBuilder();
        await foreach (var chunk in stream)
        {
            _log.LogDebug("Chunk: {Chunk}", chunk);
            sb.Append(chunk);
            yield return chunk;
        }

        _turns.Add(("assistant", sb.ToString()));
    }

    private void LogDecoding(InferenceParams p)
    {
        if (p.SamplingPipeline is DefaultSamplingPipeline sp)
        {
            _log.LogInformation(
                "Decoding for {Model}: MaxTokens={Max} T={T} TopP={TopP} TopK={TopK} MinP={MinP} RP={RP} PC={PC} Stops=[{Stops}]",
                _boundModel, p.MaxTokens, sp.Temperature, sp.TopP, sp.TopK, sp.MinP,
                sp.RepeatPenalty, sp.PenaltyCount, string.Join("|", p.AntiPrompts ?? Array.Empty<string>()));
        }
        else
        {
            _log.LogInformation(
                "Decoding for {Model}: MaxTokens={Max} Stops=[{Stops}] (non-default pipeline)",
                _boundModel, p.MaxTokens, string.Join("|", p.AntiPrompts ?? Array.Empty<string>()));
        }
    }
}
