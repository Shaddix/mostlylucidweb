using System.Text.Json;
using Mostlylucid.SegmentCommerce.Models;

namespace Mostlylucid.SegmentCommerce.Services.Attributes;

public class GadgetAttributeProvider
{
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<GadgetAttributeProvider> _logger;
    private GadgetAttributeCatalogue? _cache;

    public GadgetAttributeProvider(IWebHostEnvironment env, ILogger<GadgetAttributeProvider> logger)
    {
        _env = env;
        _logger = logger;
    }

    public async Task<GadgetAttributeCatalogue> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cache != null)
        {
            return _cache;
        }

        var path = Path.Combine(_env.ContentRootPath, "Data", "gadget-attributes.json");
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Gadget attribute catalogue not found", path);
        }

        await using var stream = File.OpenRead(path);
        _cache = await JsonSerializer.DeserializeAsync(stream, GadgetAttributeCatalogueJsonContext.Default.GadgetAttributeCatalogue, cancellationToken)
                 ?? throw new InvalidOperationException("Failed to load gadget attribute catalogue");

        return _cache;
    }
}
