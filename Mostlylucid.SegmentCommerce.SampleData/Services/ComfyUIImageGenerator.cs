using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Spectre.Console;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Generates product images using ComfyUI API.
/// </summary>
public class ComfyUIImageGenerator : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly GenerationConfig _config;
    private readonly string _clientId;
    private readonly string _workflowTemplate;

    public ComfyUIImageGenerator(HttpClient httpClient, GenerationConfig config)
    {
        _httpClient = httpClient;
        _config = config;
        _httpClient.BaseAddress = new Uri(config.ComfyUIBaseUrl);
        _httpClient.Timeout = TimeSpan.FromSeconds(config.ComfyUITimeoutSeconds);
        _clientId = Guid.NewGuid().ToString("N")[..8];

        // Load workflow template
        var workflowPath = Path.Combine(AppContext.BaseDirectory, "ComfyUI", "workflows", "product_image.json");
        _workflowTemplate = File.Exists(workflowPath)
            ? File.ReadAllText(workflowPath)
            : GetDefaultWorkflow();
    }

    /// <summary>
    /// Check if ComfyUI is available.
    /// </summary>
    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.GetAsync("/system_stats", cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Generate images for a product.
    /// </summary>
    public async Task<List<GeneratedImage>> GenerateProductImagesAsync(
        GeneratedProduct product,
        CancellationToken cancellationToken = default)
    {
        var images = new List<GeneratedImage>();

        // Ensure output directory exists
        var outputDir = Path.Combine(_config.OutputPath, product.Category, SanitizeFileName(product.Name));
        Directory.CreateDirectory(outputDir);

        // Generate main product image
        var mainImage = await GenerateImageAsync(
            product.ImagePrompt,
            outputDir,
            "main",
            cancellationToken);

        if (mainImage != null)
        {
            mainImage.IsPrimary = true;
            images.Add(mainImage);
        }

        // Generate colour variants
        foreach (var colour in product.ColourVariants.Take(_config.ImagesPerProduct - 1))
        {
            var colourPrompt = ModifyPromptForColour(product.ImagePrompt, colour);
            var variantImage = await GenerateImageAsync(
                colourPrompt,
                outputDir,
                $"colour_{SanitizeFileName(colour)}",
                cancellationToken);

            if (variantImage != null)
            {
                images.Add(variantImage);
            }
        }

        return images;
    }

    public Task<GeneratedImage?> GeneratePortraitAsync(
        string prompt,
        string profileKey,
        CancellationToken cancellationToken = default)
    {
        var outputDir = Path.Combine(_config.OutputPath, "profile-images", SanitizeFileName(profileKey));
        Directory.CreateDirectory(outputDir);
        return GenerateImageAsync(prompt, outputDir, "portrait", cancellationToken);
    }

    private async Task<GeneratedImage?> GenerateImageAsync(
        string prompt,
        string outputDir,
        string variant,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build the workflow with the prompt
            var workflow = BuildWorkflow(prompt);

            // Queue the prompt
            var promptId = await QueuePromptAsync(workflow, cancellationToken);
            if (string.IsNullOrEmpty(promptId))
            {
                AnsiConsole.WriteLine("[yellow]Failed to queue prompt[/]");
                return null;
            }

            // Wait for completion and get the image
            var imageData = await WaitForImageAsync(promptId, cancellationToken);
            if (imageData == null || imageData.Length == 0)
            {
                AnsiConsole.WriteLine("[yellow]No image data received[/]");
                return null;
            }

            // Save the image
            var fileName = $"{variant}.png";
            var filePath = Path.Combine(outputDir, fileName);
            await File.WriteAllBytesAsync(filePath, imageData, cancellationToken);

            return new GeneratedImage
            {
                FilePath = filePath,
                Variant = variant,
                IsPrimary = false
            };
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteLine($"[red]Error generating image: {ex.Message}[/]");
            return null;
        }
    }

    private JsonObject BuildWorkflow(string prompt)
    {
        var workflow = JsonNode.Parse(_workflowTemplate)?.AsObject()
                       ?? throw new InvalidOperationException("Failed to parse workflow template");

        // Find and update the positive prompt node (usually node "6" in SDXL workflows)
        foreach (var node in workflow)
        {
            if (node.Value is JsonObject nodeObj)
            {
                var classType = nodeObj["class_type"]?.GetValue<string>();

                // Update CLIPTextEncode nodes with our prompt
                if (classType == "CLIPTextEncode")
                {
                    var inputs = nodeObj["inputs"]?.AsObject();
                    if (inputs != null && inputs.ContainsKey("text"))
                    {
                        // Check if this is the positive prompt (not negative)
                        var currentText = inputs["text"]?.GetValue<string>() ?? "";
                        if (!currentText.Contains("bad") && !currentText.Contains("ugly") && !currentText.Contains("deformed"))
                        {
                            inputs["text"] = prompt;
                        }
                    }
                }

                // Update image dimensions if EmptyLatentImage node
                if (classType == "EmptyLatentImage")
                {
                    var inputs = nodeObj["inputs"]?.AsObject();
                    if (inputs != null)
                    {
                        inputs["width"] = _config.ImageWidth;
                        inputs["height"] = _config.ImageHeight;
                    }
                }
            }
        }

        return workflow;
    }

    private void TryPatchCheckpoint(JsonObject workflow, string checkpoint)
    {
        if (string.IsNullOrWhiteSpace(checkpoint))
        {
            return;
        }

        foreach (var node in workflow)
        {
            if (node.Value is JsonObject nodeObj && nodeObj["class_type"]?.GetValue<string>() == "CheckpointLoaderSimple")
            {
                var inputs = nodeObj["inputs"]?.AsObject();
                if (inputs != null)
                {
                    inputs["ckpt_name"] = checkpoint;
                }
            }
        }
    }

    private void TryPatchRefiner(JsonObject workflow, string refiner)
    {
        if (string.IsNullOrWhiteSpace(refiner))
        {
            return;
        }

        foreach (var node in workflow)
        {
            if (node.Value is JsonObject nodeObj && nodeObj["class_type"]?.GetValue<string>() == "CheckpointLoaderSimple" && node.Key == "4")
            {
                var inputs = nodeObj["inputs"]?.AsObject();
                if (inputs != null && inputs.ContainsKey("ckpt_name"))
                {
                    // only set if base checkpoint is missing
                    inputs["ckpt_name"] ??= refiner;
                }
            }
        }
    }

    private async Task<string?> QueuePromptAsync(JsonObject workflow, CancellationToken cancellationToken)
    {
        var request = new JsonObject
        {
            ["prompt"] = workflow,
            ["client_id"] = _clientId
        };

        // If no checkpoint provided in workflow, try to set a default SDXL base/refiner if present
        TryPatchCheckpoint(workflow, _config.ComfyUICheckpointName ?? "sd_xl_base_1.0.safetensors");
        TryPatchRefiner(workflow, _config.ComfyUIRefinerName ?? "sd_xl_refiner_1.0.safetensors");

        var content = new StringContent(
            request.ToJsonString(),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("/prompt", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync(cancellationToken);
            AnsiConsole.WriteLine($"[red]ComfyUI error: {error}[/]");
            return null;
        }

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var responseObj = JsonNode.Parse(responseJson);

        return responseObj?["prompt_id"]?.GetValue<string>();
    }

    private async Task<byte[]?> WaitForImageAsync(string promptId, CancellationToken cancellationToken)
    {
        // Poll for completion using the history endpoint
        var maxAttempts = 120; // 2 minutes at 1 second intervals
        for (var i = 0; i < maxAttempts; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var historyResponse = await _httpClient.GetAsync($"/history/{promptId}", cancellationToken);
            if (historyResponse.IsSuccessStatusCode)
            {
                var historyJson = await historyResponse.Content.ReadAsStringAsync(cancellationToken);
                var history = JsonNode.Parse(historyJson);

                if (history?[promptId] is JsonObject promptHistory)
                {
                    var outputs = promptHistory["outputs"]?.AsObject();
                    if (outputs != null && outputs.Count > 0)
                    {
                        // Find the SaveImage node output
                        foreach (var output in outputs)
                        {
                            if (output.Value is JsonObject outputObj)
                            {
                                var images = outputObj["images"]?.AsArray();
                                if (images != null && images.Count > 0)
                                {
                                    var imageInfo = images[0]?.AsObject();
                                    var filename = imageInfo?["filename"]?.GetValue<string>();
                                    var subfolder = imageInfo?["subfolder"]?.GetValue<string>() ?? "";

                                    if (!string.IsNullOrEmpty(filename))
                                    {
                                        return await DownloadImageAsync(filename, subfolder, cancellationToken);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            await Task.Delay(1000, cancellationToken);
        }

        return null;
    }

    private async Task<byte[]?> DownloadImageAsync(string filename, string subfolder, CancellationToken cancellationToken)
    {
        var url = $"/view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type=output";
        var response = await _httpClient.GetAsync(url, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadAsByteArrayAsync(cancellationToken);
        }

        return null;
    }

    private static string ModifyPromptForColour(string basePrompt, string colour)
    {
        // Add colour modifier to the prompt
        return $"{basePrompt}, {colour} colour variant, product colour is {colour}";
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries))
            .Replace(" ", "_")
            .ToLowerInvariant();
    }

    private static string GetDefaultWorkflow()
    {
        // A minimal SDXL workflow for product photography
        return """
        {
          "3": {
            "class_type": "KSampler",
            "inputs": {
              "cfg": 7,
              "denoise": 1,
              "latent_image": ["5", 0],
              "model": ["4", 0],
              "negative": ["7", 0],
              "positive": ["6", 0],
              "sampler_name": "euler",
              "scheduler": "normal",
              "seed": 0,
              "steps": 25
            }
          },
          "4": {
            "class_type": "CheckpointLoaderSimple",
            "inputs": {
              "ckpt_name": "sd_xl_base_1.0.safetensors"
            }
          },
          "5": {
            "class_type": "EmptyLatentImage",
            "inputs": {
              "batch_size": 1,
              "height": 512,
              "width": 512
            }
          },
          "6": {
            "class_type": "CLIPTextEncode",
            "inputs": {
              "clip": ["4", 1],
              "text": "Professional product photography, studio lighting, white background"
            }
          },
          "7": {
            "class_type": "CLIPTextEncode",
            "inputs": {
              "clip": ["4", 1],
              "text": "bad quality, blurry, distorted, ugly, deformed, low resolution, watermark, text"
            }
          },
          "8": {
            "class_type": "VAEDecode",
            "inputs": {
              "samples": ["3", 0],
              "vae": ["4", 2]
            }
          },
          "9": {
            "class_type": "SaveImage",
            "inputs": {
              "filename_prefix": "product",
              "images": ["8", 0]
            }
          }
        }
        """;
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
