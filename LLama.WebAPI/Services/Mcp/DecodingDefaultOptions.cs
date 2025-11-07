namespace Llama.WebAPI.Services.Mcp;

public sealed class DecodingDefaultsOptions
{
    public GlobalDefaults Global { get; set; } = new();
    public List<ModelDefaults> PerModel { get; set; } = new();

    public  class GlobalDefaults
    {
        public float Temperature { get; set; } = 0.6f;
        public float TopP { get; set; } = 0.95f;
        public int   TopK { get; set; } = 20;
        public float MinP { get; set; } = 0.0f;
        public float RepeatPenalty { get; set; } = 1.10f;
        public int   PenaltyLastN   { get; set; } = 64;
    }

    public sealed class ModelDefaults : GlobalDefaults
    {
        public string NamePrefix { get; set; } = string.Empty;
    }
}