using LLama.Common;

namespace LLama.WebAPI.Services
{
    public interface IChatFormatter
    {
        string FormatMessages(ChatHistory history);
        string FormatUserMessage(string message);
        string FormatSystemMessage(string message);
    }
}