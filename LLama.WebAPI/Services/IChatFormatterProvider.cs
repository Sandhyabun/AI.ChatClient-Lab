public interface IChatFormatter
{
    string Render(string system, IEnumerable<(string role, string content)> turns, string nextRole = "assistant");
}

public interface IChatFormatterProvider
{
    IChatFormatter For(string modelName);
}