namespace TinyLLM.Models;

public enum ModelSource
{
    Ollama,
    LocalFile
}

public class ModelInfo
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public ModelSource Source { get; set; }
    public string Path { get; set; } = "";
    public long Size { get; set; }
    public string Description { get; set; } = "";

    public override string ToString() => DisplayName;
}
