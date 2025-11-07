namespace LLama.Web.Common;

public class LLamaOptions
{
    public ModelLoadType ModelLoadType { get; set; }
    public List<ModelOptions> Models { get; set; } = new();
    public string DefaultModel { get; set; }
}