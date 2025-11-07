using Microsoft.AspNetCore.Mvc;
using LLama.WebAPI.Services;

namespace LLama.WebAPI.Controllers;

[ApiController]
[Route("api/models")]
public sealed class ModelsController : ControllerBase
{
    private readonly ModelManager _models;
    public ModelsController(ModelManager models) => _models = models;

    [HttpGet]
    public IActionResult List()
        => Ok(new { current = _models.CurrentModelName, available = _models.ListModels() });

    public record SelectReq(string name);

    [HttpPost("select")]
    public IActionResult Select([FromBody] SelectReq req)
    {
        try
        {
            _models.Switch(req.name);
            return Ok(new { current = _models.CurrentModelName });
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("unload")]
    public IActionResult Unload([FromBody] SelectReq req)
    {
        try
        {
            _models.Unload(req.name);
            return Ok();
        }
        catch (Exception ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}