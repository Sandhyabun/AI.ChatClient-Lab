using LLama.Common;
using System.Text;
namespace LLama.WebAPI.Services
{
    public class ChatFormatterProvider : IChatFormatterProvider
    {
        private readonly Dictionary<string, IChatFormatter> _formatters;

        public ChatFormatterProvider()
        {
            _formatters = new Dictionary<string, IChatFormatter>(StringComparer.OrdinalIgnoreCase)
            {
                ["Qwen"] = new QwenChatFormatter(),
                ["Phi-3"] = new Phi3ChatFormatter(),
                ["TinyLlama"] = new AlpacaChatFormatter(),
                ["default"] = new DefaultChatFormatter()
            };
        }

        public IChatFormatter GetFormatter(string modelName)
        {
            // Find the first matching formatter based on model name prefix
            foreach (var kvp in _formatters)
            {
                if (modelName.StartsWith(kvp.Key, StringComparison.OrdinalIgnoreCase))
                {
                    return kvp.Value;
                }
            }

            return _formatters["default"];
        }
    }

    // Qwen format: <|im_start|>role\ncontent<|im_end|>
    internal class QwenChatFormatter : IChatFormatter
    {
        public string FormatMessages(ChatHistory history)
        {
            var sb = new StringBuilder();
            foreach (var msg in history.Messages ?? Enumerable.Empty<ChatHistory.Message>())
            {
                sb.AppendLine($"<|im_start|>{msg.AuthorRole}");
                sb.AppendLine(msg.Content);
                sb.AppendLine("<|im_end|>");
            }
            return sb.ToString();
        }

        public string FormatUserMessage(string message) 
            => $"<|im_start|>user\n{message}\n<|im_end|>\n<|im_start|>assistant\n";

        public string FormatSystemMessage(string message) 
            => $"<|im_start|>system\n{message}\n<|im_end|>\n";
    }

    // Phi-3 format: <|role|>\ncontent<|end|>
    internal class Phi3ChatFormatter : IChatFormatter
    {
        public string FormatMessages(ChatHistory history)
        {
            var sb = new StringBuilder();
            foreach (var msg in history.Messages ?? Enumerable.Empty<ChatHistory.Message>())
            {
                sb.AppendLine($"<|{msg.AuthorRole}|>");
                sb.AppendLine(msg.Content);
                sb.AppendLine("<|end|>");
            }
            return sb.ToString();
        }

        public string FormatUserMessage(string message) 
            => $"<|user|>\n{message}\n<|end|>\n<|assistant|>\n";

        public string FormatSystemMessage(string message) 
            => $"<|system|>\n{message}\n<|end|>\n";
    }

    // Alpaca/TinyLlama format
    internal class AlpacaChatFormatter : IChatFormatter
    {
        public string FormatMessages(ChatHistory history)
        {
            var sb = new StringBuilder();
            foreach (var msg in history.Messages ?? Enumerable.Empty<ChatHistory.Message>())
            {
                var header = msg.AuthorRole.ToString() == "System" 
                    ? "### Instruction:" 
                    : msg.AuthorRole.ToString() == "User" 
                        ? "### Input:" 
                        : "### Response:";
                
                sb.AppendLine(header);
                sb.AppendLine(msg.Content);
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public string FormatUserMessage(string message) 
            => $"### Input:\n{message}\n\n### Response:\n";

        public string FormatSystemMessage(string message) 
            => $"### Instruction:\n{message}\n\n";
    }

    // Default/generic format
    internal class DefaultChatFormatter : IChatFormatter
    {
        public string FormatMessages(ChatHistory history)
        {
            var sb = new StringBuilder();
            foreach (var msg in history.Messages ?? Enumerable.Empty<ChatHistory.Message>())
            {
                sb.AppendLine($"{msg.AuthorRole}: {msg.Content}");
            }
            return sb.ToString();
        }

        public string FormatUserMessage(string message) 
            => $"User: {message}\nAssistant: ";

        public string FormatSystemMessage(string message) 
            => $"System: {message}\n";
    }
}