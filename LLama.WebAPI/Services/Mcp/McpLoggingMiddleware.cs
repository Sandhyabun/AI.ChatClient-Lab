using System.Text;

namespace LLama.WebAPI.Services;

public class McpLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<McpLoggingMiddleware> _logger;

    public McpLoggingMiddleware(RequestDelegate next, ILogger<McpLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        //  Request 
        context.Request.EnableBuffering();
        var requestBody = await ReadRequestBodyAsync(context.Request);
        _logger.LogInformation("==== MCP REQUEST ====");
        _logger.LogInformation("Method: {Method}", context.Request.Method);
        _logger.LogInformation("Path: {Path}", context.Request.Path);
        _logger.LogInformation("Query: {Query}", context.Request.QueryString);
        _logger.LogInformation("Body: {Body}", requestBody);
        _logger.LogInformation("====================");

        //Response wrapping
        var originalBodyStream = context.Response.Body;
        using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);

            // Success path: read & log buffered response, then copy to original
            var responseBodyText = await ReadResponseBodyAsync(context.Response);
            _logger.LogInformation("==== MCP RESPONSE ====");
            _logger.LogInformation("StatusCode: {StatusCode}", context.Response.StatusCode);
            _logger.LogInformation("Body: {Body}", responseBodyText);
            _logger.LogInformation("=====================");

            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            // Put original stream back so the DeveloperExceptionPage can write to it
            context.Response.Body = originalBodyStream;


            responseBody.Position = 0;
            var partial = await new StreamReader(responseBody, Encoding.UTF8).ReadToEndAsync();
            _logger.LogError(ex, "Unhandled exception in pipeline. Partial response so far: {Body}", partial);

            throw;
        }
        finally
        {
            // Always restore the original stream
            context.Response.Body = originalBodyStream;
            
        }
    }

    private static async Task<string> ReadRequestBodyAsync(HttpRequest request)
    {
        request.Body.Position = 0;
        using var reader = new StreamReader(request.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        request.Body.Position = 0;
        return string.IsNullOrEmpty(body) ? "(empty)" : body;
    }

    private static async Task<string> ReadResponseBodyAsync(HttpResponse response)
    {
        response.Body.Position = 0;
        using var reader = new StreamReader(response.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        response.Body.Position = 0;
        return string.IsNullOrEmpty(body) ? "(empty)" : body;
    }
}