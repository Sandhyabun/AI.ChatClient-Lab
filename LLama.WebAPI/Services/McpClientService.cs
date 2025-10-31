using LLama.WebAPI.Models;
using LLama.Common;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Json;

namespace LLama.WebAPI.Services
{
    public class McpClientService
    {
        private readonly HttpClient _httpClient;
        private readonly McpSettings _settings;
        private readonly ILogger<McpClientService> _logger;

        public McpClientService(HttpClient httpClient, McpSettings settings, ILogger<McpClientService> logger)
        {
            _httpClient = httpClient;
            _settings = settings;
            _logger = logger;

            // Configure base URL
            _httpClient.BaseAddress = new Uri(_settings.ServerUrl);

            if (!string.IsNullOrEmpty(_settings.ApiKey))
            {
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_settings.ApiKey}");
            }
        }

        // Generic POST request to MCP endpoint
        public async Task<string> SendAsync<T>(T requestBody)
        {
            try
            {
                _logger.LogInformation("Sending MCP request to {Url}", _settings.ServerUrl);

                var response = await _httpClient.PostAsJsonAsync("/Chat/Send", requestBody);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("MCP response received: {Result}", result);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "MCP request failed");
                return $"Error: {ex.Message}";
            }
        }
    }
}