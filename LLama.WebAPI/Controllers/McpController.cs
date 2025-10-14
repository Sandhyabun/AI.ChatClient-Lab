using LLama.WebAPI.Services;
using Microsoft.AspNetCore.Mvc;
using LLama.WebAPI.Models;

namespace LLama.WebAPI.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class McpController : ControllerBase
    { 
                       private readonly McpClientService _mcpClient;
                        private readonly ILogger<McpController> _logger;
                
                        public McpController(McpClientService mcpClient, ILogger<McpController> logger)
                        {
                            _mcpClient = mcpClient;
                            _logger = logger;
                        }
                
                        [HttpPost("send")]
                        public async Task<IActionResult> SendToMcp([FromBody] SendMessageInput input)
                        {
                            _logger.LogInformation("Incoming MCP request: {Input}", input);
                
                            // forward exactly as-is
                            var result = await _mcpClient.SendAsync(input);
                            return Ok(result);
                        }
                       
                    }

   
   
}