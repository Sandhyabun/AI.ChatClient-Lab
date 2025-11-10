using LLama;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;

namespace LLama.WebAPI.Services
{
    public class ModelManager : IDisposable
    {
        private readonly IConfiguration _config;
        private readonly ILogger<ModelManager> _logger;

        // keep both name and path
        private readonly List<(string Name, string Path)> _models = new();
        private LLamaWeights? _weights;
        private LLamaContext? _context;
        private ChatSession? _chatSession;

        private string? _currentModelPath;

        public ModelManager(IConfiguration config, ILogger<ModelManager> logger)
        {
            _config = config;
            _logger = logger;

            //  Read from LLama:Models
            var sections = _config.GetSection("LLama:Models").GetChildren();

            foreach (var s in sections)
            {
                var path = s.GetValue<string>("ModelPath");
                if (string.IsNullOrWhiteSpace(path)) continue;

                var name = s.GetValue<string>("Name") ?? System.IO.Path.GetFileName(path);
                _models.Add((name, path));
            }

            _logger.LogInformation(" Available models ({Count}): {List}",
                _models.Count, string.Join(", ", _models.Select(m => $"{m.Name} -> {m.Path}")));
        }

        public IEnumerable<string> GetAvailableModels() => _models.Select(m => m.Name);
        public bool IsModelLoaded => _context != null;
        public string? CurrentModelPath => _currentModelPath;

        private string? ResolvePath(string nameOrPath)
        {
            // if caller already sent a full path
            if (System.IO.Path.IsPathRooted(nameOrPath))
                return nameOrPath;

            // otherwise try by name (case-insensitive)
            return _models
                .FirstOrDefault(m => string.Equals(m.Name, nameOrPath, StringComparison.OrdinalIgnoreCase))
                .Path;
        }

        public void LoadModel(string nameOrPath)
        {
            var modelPath = ResolvePath(nameOrPath);

            if (string.IsNullOrWhiteSpace(modelPath) || !System.IO.File.Exists(modelPath))
            {
                _logger.LogError(" Model file not found: {Path}", nameOrPath);
                throw new FileNotFoundException($"Model file not found: {nameOrPath}");
            }

            // dispose prior model
            _weights?.Dispose();
            _context?.Dispose();
            _chatSession = null;

            _logger.LogInformation(" Loading model from: {Path}", modelPath);

            var mp = new ModelParams(modelPath) { ContextSize = 4096 };
            _weights = LLamaWeights.LoadFromFile(mp);
            _context = _weights.CreateContext(mp);
            _chatSession = new ChatSession(new InteractiveExecutor(_context));
            _chatSession.History.AddMessage(AuthorRole.System, "You are a helpful assistant powered by LLamaSharp.");

            _currentModelPath = modelPath;
            _logger.LogInformation("Model loaded: {File}", System.IO.Path.GetFileName(modelPath));
        }

        public async Task<string> GenerateAsync(string prompt)
        {
            if (_chatSession == null) return " No model loaded.";

            var outputs = _chatSession.ChatAsync(
                new LLama.Common.ChatHistory.Message(AuthorRole.User, prompt),
                new InferenceParams { AntiPrompts = ["User:"], MaxTokens = 200, SamplingPipeline = new DefaultSamplingPipeline() });

            var result = "";
            await foreach (var t in outputs) result += t;
            return result;
        }

        public void Dispose()
        {
            _weights?.Dispose();
            _context?.Dispose();
        }
    }
}
