using Microsoft.AspNetCore.Mvc;
using LLama.WebAPI.Services;

namespace LLama.WebAPI.Controllers
{
    [ApiController]
    [Route("api/models")]
    public class ModelController : ControllerBase
    {
        private readonly ModelManager _modelManager;
        private readonly ILogger<ModelController> _logger;

        public ModelController(ModelManager modelManager, ILogger<ModelController> logger)
        {
            _modelManager = modelManager;
            _logger = logger;
        }

        [HttpPost("load")]
        public IActionResult LoadModel([FromBody] string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                return BadRequest(new { error = "Model path cannot be empty." });

            if (!System.IO.File.Exists(modelPath))
                return BadRequest(new { error = $"Model file does not exist at path: {modelPath}" });

            try
            {
                _modelManager.LoadModel(modelPath);
                return Ok($" Loaded model: {Path.GetFileName(modelPath)}");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = $"Model load failed: {ex.Message}" });
            }
        }

        [HttpPost("generate")]
        public async Task<IActionResult> Generate([FromBody] string prompt)
        {
            var response = await _modelManager.GenerateAsync(prompt);
            return Ok(response);
        }

        [HttpGet("status")]
        public IActionResult GetStatus()
        {
            return Ok(new
            {
                currentModel = _modelManager.CurrentModelPath ?? "None",
                isLoaded = _modelManager.IsModelLoaded
            });
        }

        [HttpGet("list")]
        public IActionResult GetModels()
        {
            var models = _modelManager.GetAvailableModels();
            return Ok(models);
        }
    }
}