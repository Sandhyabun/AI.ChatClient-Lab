using System;
using System.Collections.Concurrent;
using System.Linq;
using LLama;
using LLama.Common;
using LLama.Web.Common;
using LLama.Web.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LLama.WebAPI.Services
{
    public sealed class ModelManager : IDisposable
    {
        private readonly ILogger<ModelManager> _log;
        private readonly IOptions<LLamaOptions> _options;

        private readonly ConcurrentDictionary<string, LLamaContext> _contexts = new();
        private readonly ConcurrentDictionary<string, LLamaModel>   _models   = new();

        // NEW: track in-flight uses per model
        private readonly ConcurrentDictionary<string, int> _inflight = new();

        private readonly object _switchLock = new();
        private string? _currentName;

        public ModelManager(ILogger<ModelManager> log, IOptions<LLamaOptions> options)
        { /* ... your existing ctor unchanged ... */ }

        public string? CurrentModelName => _currentName;

        public (LLamaModel model, LLamaContext ctx) GetCurrent()
        {
            if (string.IsNullOrWhiteSpace(_currentName))
                throw new InvalidOperationException("No model loaded yet. Call /api/models/select.");

            return (_models[_currentName], _contexts[_currentName]);
        }

        public (LLamaModel model, LLamaContext ctx) GetOrCreate(string name)
        {
            EnsureConfigured(name);
            if (!_models.ContainsKey(name))
                Load(name);
            return (_models[name], _contexts[name]);
        }

        public void Switch(string name)
        {
            EnsureConfigured(name);
            lock (_switchLock)
            {
                if (!_models.ContainsKey(name))
                    Load(name);

                _currentName = name;
                _log.LogInformation("Switched active model to {Name}", _currentName);
            }
        }

        public void LoadAndSwitch(string name)
        {
            EnsureConfigured(name);
            lock (_switchLock)
            {
                if (!_models.ContainsKey(name))
                    Load(name);

                _currentName = name;
                _log.LogInformation("Loaded and switched to {Name}", _currentName);
            }
        }

        // === NEW: ContextLease so callers keep the context alive while generating ===
        public readonly struct ContextLease : IDisposable
        {
            private readonly ModelManager _mm;
            public string Name { get; }
            public LLamaContext Context { get; }

            internal ContextLease(ModelManager mm, string name, LLamaContext ctx)
            {
                _mm = mm; Name = name; Context = ctx;
                _mm._inflight.AddOrUpdate(name, 1, (_, n) => n + 1);
            }

            public void Dispose()
            {
                _mm._inflight.AddOrUpdate(Name, 0, (_, n) => Math.Max(0, n - 1));
            }
        }

        // === NEW: acquire a lease for the current (or a specific) model ===
        public ContextLease AcquireCurrent()
        {
            if (string.IsNullOrWhiteSpace(_currentName))
                throw new InvalidOperationException("No model loaded yet.");
            var name = _currentName!;
            var (_, ctx) = GetOrCreate(name);
            return new ContextLease(this, name, ctx);
        }

        public ContextLease Acquire(string name)
        {
            var (_, ctx) = GetOrCreate(name);
            return new ContextLease(this, name, ctx);
        }

        public void Unload(string name)
        {
            // Don’t allow unloading the current model; switch first.
            if (string.Equals(name, _currentName, StringComparison.Ordinal))
                throw new InvalidOperationException("Cannot unload the current active model. Switch to another model first.");

            // NEW: refuse to unload if in use
            if (_inflight.TryGetValue(name, out var n) && n > 0)
                throw new InvalidOperationException($"Model '{name}' is busy (in-flight={n}). Try again later.");

            if (_contexts.TryRemove(name, out var ctx))
            {
                _log.LogInformation("Unloading context for {Name}", name);
                ctx.Dispose();
            }
            if (_models.TryRemove(name, out var model))
            {
                _log.LogInformation("Unloading model {Name}", name);
                model.Dispose();
            }
        }

        public string[] ListModels()
            => (_options.Value?.Models ?? new())
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Name))
                .Select(m => m!.Name!)
                .ToArray();

        private void Load(string name)
        {
            var cfg = FindConfig(name);
            _log.LogInformation("Loading model {Name} from {Path}", cfg.Name, cfg.ModelPath);

            var mo = new ModelOptions
            {
                ModelPath     = cfg.ModelPath,
                ContextSize   = cfg.ContextSize > 0 ? cfg.ContextSize : 4096,
                MaxInstances  = cfg.MaxInstances > 0 ? cfg.MaxInstances : 1,
                GpuLayerCount = cfg.GpuLayerCount
            };

            var model = LLamaModel.CreateAsync(mo, _log).GetAwaiter().GetResult();
            var ctx   = model.CreateContext(Guid.NewGuid().ToString()).GetAwaiter().GetResult();

            _models[cfg.Name]   = model;
            _contexts[cfg.Name] = ctx;

            _log.LogInformation("Model {Name} loaded.", cfg.Name);
        }

        private ModelOptions FindConfig(string name) => EnsureConfigured(name);

        private ModelOptions EnsureConfigured(string name)
        {
            var models = _options.Value?.Models ?? new();
            var cfg = models
                .Where(m => m != null && !string.IsNullOrWhiteSpace(m.Name))
                .FirstOrDefault(m => string.Equals(m!.Name, name, StringComparison.OrdinalIgnoreCase));

            if (cfg is null)
                throw new InvalidOperationException(
                    $"Model '{name}' not found. Available: {string.Join(", ", ListModels())}");

            return cfg!;
        }

        public void Dispose()
        {
            foreach (var c in _contexts.Values) c.Dispose();
            foreach (var m in _models.Values)   m.Dispose();
            _contexts.Clear();
            _models.Clear();
        }
    }
}
