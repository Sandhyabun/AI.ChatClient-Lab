using LLama.Common;
using LLama.WebAPI.Services;
using LLama.WebAPI.Models;
using Microsoft.AspNetCore.Mvc;

namespace LLama.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class McpController : ControllerBase
{
    private readonly StatelessChatService _chatService;
    private readonly ILogger<McpController> _logger;

    public McpController(StatelessChatService chatService, ILogger<McpController> logger)
    {
        _chatService = chatService;
        _logger = logger;
    }

    [HttpPost("send")]
    public async Task<IActionResult> SendToMcp([FromBody] SendMessageInput input)
    {
        _logger.LogInformation("Incoming MCP stateless request: {Input}", input);
        
        // Build a new chat history for each request
        var history = new ChatHistory();
        history.AddMessage(AuthorRole.User, input.Text);
        
        // ✅ Stateless chat → new session every time
        var result = await _chatService.SendAsync(history);
        return Ok(result);
    }
}