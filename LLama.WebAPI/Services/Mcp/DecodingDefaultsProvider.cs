#nullable enable
using System;
using System.Linq;
using LLama.Common;
using LLama.Sampling;
using Microsoft.Extensions.Options;

namespace LLama.WebAPI.Services
{
 
    public interface IDecodingDefaultsProvider
    {
        InferenceParams For(string modelName);
    }

    public sealed class DecodingDefaultsProvider : IDecodingDefaultsProvider
    {
        private readonly DecodingDefaultsOptions _opt;

        public DecodingDefaultsProvider(IOptions<DecodingDefaultsOptions> opt)
        {
            _opt = opt?.Value ?? throw new ArgumentNullException(nameof(opt));
        }

        public InferenceParams For(string modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                throw new ArgumentException("Model name is required.", nameof(modelName));

            // Match the first model-specific block whose prefix matches the selected model name.
            var perModel = _opt.ModelDefaults?.FirstOrDefault(md =>
                modelName.StartsWith(md.NamePrefix, StringComparison.OrdinalIgnoreCase));

            var src = perModel ?? _opt.Global ?? new DecodingPreset();

            // Build the sampling pipeline (keep it simple & explicit)
            var sampling = new DefaultSamplingPipeline
            {
                Temperature   = src.Temperature,
                TopP          = src.TopP,
                TopK          = src.TopK,
                MinP          = src.MinP,
                RepeatPenalty = src.RepeatPenalty,
                PenaltyCount  = src.PenaltyCount
            };

            return new InferenceParams
            {
                SamplingPipeline = sampling,
                MaxTokens        = src.MaxTokens,
                AntiPrompts      = StopStrings(modelName)
            };
        }

       
        private static string[] StopStrings(string modelName)
        {
            // Qwen 2.x Instruct (GGUF) – uses <|im_*|> template.
            if (modelName.StartsWith("Qwen", StringComparison.OrdinalIgnoreCase))
                return new[] { "<|im_end|>", "<|im_start|>user", "<|im_start|>system" };

            // Phi-3 Instruct (GGUF) – uses <|user|>/<|assistant|> with <|end|>.
            if (modelName.StartsWith("Phi-3", StringComparison.OrdinalIgnoreCase))
                return new[] { "<|end|>", "<|user|>" };

            // TinyLlama / Alpaca-like – section headers.
            if (modelName.StartsWith("TinyLlama", StringComparison.OrdinalIgnoreCase))
                return new[] { "### Instruction:", "### Input:", "### Response:" };

            // Fallback: no extra stops.
            return Array.Empty<string>();
        }
    }

    
    public sealed class DecodingDefaultsOptions
    {
        public DecodingPreset? Global { get; set; }
        public DecodingPreset[]? ModelDefaults { get; set; }
    }

 
    public sealed class DecodingPreset
    {
        // Sampling
        public float Temperature   { get; set; } = 0.7f;
        public float TopP          { get; set; } = 0.9f;
        public int   TopK          { get; set; } = 40;
        public float MinP          { get; set; } = 0.05f;

        // Repetition control
        public float RepeatPenalty { get; set; } = 1.15f;
        public int   PenaltyCount  { get; set; } = 128;

        // Decoding cap
        public int   MaxTokens     { get; set; } = 256;

        // Per-model selection
        public string NamePrefix   { get; set; } = string.Empty;
    }
}
