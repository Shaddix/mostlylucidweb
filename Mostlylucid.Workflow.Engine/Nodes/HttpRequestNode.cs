using System.Net.Http.Json;
using System.Text.Json;
using Mostlylucid.Workflow.Engine.Interfaces;
using Mostlylucid.Workflow.Shared.Models;

namespace Mostlylucid.Workflow.Engine.Nodes;

/// <summary>
/// Node that makes HTTP requests
/// </summary>
public class HttpRequestNode : BaseWorkflowNode
{
    private readonly IHttpClientFactory _httpClientFactory;

    public HttpRequestNode(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public override string NodeType => "HttpRequest";

    public override async Task<NodeExecutionResult> ExecuteAsync(
        WorkflowNode nodeConfig,
        WorkflowExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Resolve input templates
            var resolvedInputs = ResolveTemplates(nodeConfig.Inputs, context);

            var url = resolvedInputs.GetValueOrDefault("url")?.ToString();
            var method = resolvedInputs.GetValueOrDefault("method")?.ToString() ?? "GET";
            var headers = resolvedInputs.GetValueOrDefault("headers") as Dictionary<string, object>;
            var body = resolvedInputs.GetValueOrDefault("body");

            if (string.IsNullOrEmpty(url))
            {
                return CreateFailureResult(nodeConfig, "URL is required", resolvedInputs);
            }

            var client = _httpClientFactory.CreateClient();

            // Add headers
            if (headers != null)
            {
                foreach (var (key, value) in headers)
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(key, value?.ToString() ?? string.Empty);
                }
            }

            // Make request
            HttpResponseMessage response;
            switch (method.ToUpper())
            {
                case "GET":
                    response = await client.GetAsync(url, cancellationToken);
                    break;
                case "POST":
                    response = await client.PostAsJsonAsync(url, body, cancellationToken);
                    break;
                case "PUT":
                    response = await client.PutAsJsonAsync(url, body, cancellationToken);
                    break;
                case "DELETE":
                    response = await client.DeleteAsync(url, cancellationToken);
                    break;
                default:
                    return CreateFailureResult(nodeConfig, $"Unsupported HTTP method: {method}", resolvedInputs);
            }

            // Read response
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            // Try to parse as JSON, fall back to string
            object parsedBody;
            try
            {
                parsedBody = (object?)JsonSerializer.Deserialize<Dictionary<string, object>>(responseBody) ?? responseBody;
            }
            catch
            {
                parsedBody = responseBody;
            }

            var outputData = new Dictionary<string, object>
            {
                ["statusCode"] = (int)response.StatusCode,
                ["body"] = parsedBody,
                ["headers"] = response.Headers.ToDictionary(h => h.Key, h => string.Join(", ", h.Value)),
                ["isSuccess"] = response.IsSuccessStatusCode
            };

            // Add outputs to context based on node configuration
            foreach (var (outputKey, templateValue) in nodeConfig.Outputs)
            {
                var resolvedValue = ResolveTemplate(templateValue, new WorkflowExecutionContext
                {
                    Data = outputData,
                    Execution = context.Execution,
                    Workflow = context.Workflow,
                    Services = context.Services
                });

                context.Data[outputKey] = resolvedValue;
            }

            return CreateSuccessResult(nodeConfig, outputData, resolvedInputs);
        }
        catch (Exception ex)
        {
            return CreateFailureResult(nodeConfig, ex.Message, nodeConfig.Inputs);
        }
    }

    public override async Task<List<string>> ValidateAsync(WorkflowNode nodeConfig)
    {
        var errors = await base.ValidateAsync(nodeConfig);

        if (!nodeConfig.Inputs.ContainsKey("url"))
        {
            errors.Add("HttpRequest node requires 'url' input");
        }

        return errors;
    }
}
