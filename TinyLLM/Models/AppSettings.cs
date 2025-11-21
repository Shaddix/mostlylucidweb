namespace TinyLLM.Models;

public class AppSettings
{
    public bool UseGpu { get; set; } = false;
    public int GpuLayers { get; set; } = 35; // Number of layers to offload to GPU
    public int ContextSize { get; set; } = 2048;
    public float Temperature { get; set; } = 0.7f;
    public int MaxTokens { get; set; } = 512;
    public string ModelPath { get; set; } = "";
    public int TopRagResults { get; set; } = 3;
}
