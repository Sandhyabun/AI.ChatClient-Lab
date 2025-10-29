using System.Text.Json.Serialization;

namespace LLama.WebAPI.Models
{
    public class SendMessageInput
    {

        public string Text { get; set; } = "";

        public string? UserId { get; set; }

        public string Model { get; set; } = "default";

        [JsonPropertyName("prompt")]
        public string? Prompt
        {
            get => Text;
            set => Text = value ?? "";
        }

        [JsonPropertyName("params")]
        public Dictionary<string, object>? Params { get; set; }

        // Optional structured history
        public HistoryInput? History { get; set; }
    }

    public class HistoryInput
    {
        public List<HistoryItem> Messages { get; set; } = new();

        public class HistoryItem
        {
            public string Role { get; set; } = "User";  // "User" | "Assistant" | "System"
            public string Content { get; set; } = "";
        }
    }
}