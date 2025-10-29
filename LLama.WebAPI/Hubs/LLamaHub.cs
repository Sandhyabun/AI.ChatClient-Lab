using LLama.WebAPI.Models;
using Microsoft.AspNetCore.SignalR;
using LLama.WebAPI.Services;

namespace LLama.WebAPI.Hubs
{
    public class LlamaHub : Hub
    {
        private readonly StatefulChatService _chatService;

        public LlamaHub(StatefulChatService chatService)
        {
            _chatService = chatService;
        }

        public Task PingServer(string message)
        {
            Console.WriteLine($"Ping received from client: {message}");
            return Task.CompletedTask;
        }

        public async IAsyncEnumerable<string> StreamResponse(string prompt)
        {
            Console.WriteLine($"[Hub] Streaming started for prompt: {prompt}");

            await foreach (var chunk in _chatService.SendStream(new SendMessageInput { Text = prompt }))
            
            {
                yield return chunk;
            }

            Console.WriteLine($"[Hub] Streaming complete for prompt: {prompt}");
        }
    }
}