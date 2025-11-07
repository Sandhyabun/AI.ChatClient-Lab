using LLama.Sampling;
using LLama.Web.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering; 
using Microsoft.Extensions.Options;

namespace LLama.Web.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger, IOptions<LLamaOptions> options)
        {
            _logger = logger;
            Options = options.Value ?? new LLamaOptions();
            Options.Models ??= new List<ModelOptions>();
        }

        public LLamaOptions Options { get; set; }

        // Selected model name from the dropdown
        [BindProperty] public string SelectedModel { get; set; } = string.Empty;

        // Items for the dropdown (never null)
        public List<SelectListItem> ModelItems { get; private set; } = new();

        [BindProperty] public ISessionConfig SessionConfig { get; set; } = default!;

        [BindProperty] public InferenceOptions InferenceOptions { get; set; } = default!;

        public void OnGet()
        {
            // pick a default model so the dropdown has a selection (optional)
            var defaultModelName = Options.Models.FirstOrDefault()?.Name ?? "";

            SessionConfig = new SessionConfig
            {
                Model = defaultModelName, // <-- optional but nice
                Prompt =
                    "Below is an instruction that describes a task. Write a response that appropriately completes the request.",
                AntiPrompt = "User:",
                OutputFilter = "User:, Assistant: "
            };

            InferenceOptions = new InferenceOptions
            {
                SamplingPipeline = new DefaultSamplingPipeline { Temperature = 0.8f }
            };
        }
    }
}    
