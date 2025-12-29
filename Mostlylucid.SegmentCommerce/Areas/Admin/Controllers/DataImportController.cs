using System.IO.Compression;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Services.Segments;

namespace Mostlylucid.SegmentCommerce.Areas.Admin.Controllers;

/// <summary>
/// API for importing sample data from compressed archives.
/// Supports drag-drop upload of .zip files containing JSON data.
/// </summary>
[Area("Admin")]
[Route("admin/api/[controller]")]
[ApiController]
public class DataImportController : ControllerBase
{
    private readonly SegmentCommerceDbContext _db;
    private readonly ISegmentGeneratorService _segmentGenerator;
    private readonly ILogger<DataImportController> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public DataImportController(
        SegmentCommerceDbContext db,
        ISegmentGeneratorService segmentGenerator,
        ILogger<DataImportController> logger)
    {
        _db = db;
        _segmentGenerator = segmentGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Upload and import a compressed data archive.
    /// Expects a .zip file containing JSON files:
    /// - products.json: Array of products
    /// - profiles.json: Array of persistent profiles (optional)
    /// - categories.json: Array of categories (optional)
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Policy = "AdminOnly")]
    [RequestSizeLimit(100_000_000)] // 100MB limit
    public async Task<IActionResult> UploadArchive(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { error = "No file provided" });

        if (!file.FileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "File must be a .zip archive" });

        var result = new ImportResult();

        try
        {
            await using var stream = file.OpenReadStream();
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);

            // Process each known file type
            foreach (var entry in archive.Entries)
            {
                var name = entry.Name.ToLowerInvariant();
                
                await using var entryStream = entry.Open();
                using var reader = new StreamReader(entryStream);
                var json = await reader.ReadToEndAsync(ct);

                switch (name)
                {
                    case "products.json":
                        result.ProductsImported = await ImportProductsAsync(json, ct);
                        break;
                    case "profiles.json":
                        result.ProfilesImported = await ImportProfilesAsync(json, ct);
                        break;
                    case "categories.json":
                        result.CategoriesImported = await ImportCategoriesAsync(json, ct);
                        break;
                    case "demo-users.json":
                        result.DemoUsersImported = await ImportDemoUsersAsync(json, ct);
                        break;
                }
            }

            // Regenerate segments based on new data
            if (result.ProductsImported > 0)
            {
                await _segmentGenerator.SeedDefaultSegmentsAsync(ct);
                result.SegmentsGenerated = await _db.Segments.CountAsync(ct);
            }

            result.Success = true;
            _logger.LogInformation("Import completed: {Result}", result);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Import failed");
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get the current data counts for display.
    /// </summary>
    [HttpGet("stats")]
    [AllowAnonymous]
    public async Task<IActionResult> GetStats(CancellationToken ct)
    {
        var stats = new
        {
            products = await _db.Products.CountAsync(ct),
            categories = await _db.Categories.CountAsync(ct),
            profiles = await _db.PersistentProfiles.CountAsync(ct),
            segments = await _db.Segments.CountAsync(ct),
            demoUsers = await _db.DemoUsers.CountAsync(ct),
            signals = await _db.Signals.CountAsync(ct)
        };

        return Ok(stats);
    }

    /// <summary>
    /// Trigger segment regeneration from current data.
    /// </summary>
    [HttpPost("regenerate-segments")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> RegenerateSegments(CancellationToken ct)
    {
        // Clear existing segments
        var existing = await _db.Segments.ToListAsync(ct);
        _db.Segments.RemoveRange(existing);
        await _db.SaveChangesAsync(ct);

        // Generate new segments
        await _segmentGenerator.SeedDefaultSegmentsAsync(ct);

        var count = await _db.Segments.CountAsync(ct);
        return Ok(new { segmentsGenerated = count });
    }

    /// <summary>
    /// Clear all data (dangerous - requires confirmation).
    /// </summary>
    [HttpPost("clear-all")]
    [Authorize(Policy = "AdminOnly")]
    public async Task<IActionResult> ClearAllData([FromQuery] bool confirm, CancellationToken ct)
    {
        if (!confirm)
            return BadRequest(new { error = "Must confirm with ?confirm=true" });

        // Clear in dependency order
        await _db.Database.ExecuteSqlRawAsync("TRUNCATE signals, interaction_events, order_items, orders, persistent_profiles, profile_keys, visitor_profiles, segments, demo_users RESTART IDENTITY CASCADE", ct);
        
        _logger.LogWarning("All user data cleared by admin");

        return Ok(new { message = "All data cleared" });
    }

    private async Task<int> ImportProductsAsync(string json, CancellationToken ct)
    {
        var products = JsonSerializer.Deserialize<List<ProductImportDto>>(json, JsonOptions);
        if (products == null || products.Count == 0) return 0;

        // Get or create a default seller for imports
        var defaultSeller = await GetOrCreateDefaultSellerAsync(ct);

        var count = 0;
        foreach (var dto in products)
        {
            // Check if product already exists by handle
            if (await _db.Products.AnyAsync(p => p.Handle == dto.Handle, ct))
                continue;

            var product = new ProductEntity
            {
                Handle = dto.Handle,
                Name = dto.Title,
                Description = dto.Description ?? string.Empty,
                Price = dto.Price,
                CompareAtPrice = dto.CompareAtPrice,
                Category = dto.Category ?? "uncategorized",
                Subcategory = dto.Subcategory,
                Tags = dto.Tags?.ToList() ?? [],
                ImageUrl = dto.ImageUrl ?? string.Empty,
                Status = ProductStatus.Active,
                IsFeatured = dto.IsFeatured,
                IsTrending = dto.IsTrending,
                SellerId = defaultSeller.Id
            };

            _db.Products.Add(product);
            count++;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<UserEntity> GetOrCreateDefaultSellerAsync(CancellationToken ct)
    {
        const string defaultSellerEmail = "import@segmentcommerce.demo";
        
        var seller = await _db.Users
            .Include(u => u.SellerProfile)
            .FirstOrDefaultAsync(u => u.Email == defaultSellerEmail, ct);

        if (seller != null)
            return seller;

        seller = new UserEntity
        {
            Email = defaultSellerEmail,
            DisplayName = "Import Bot",
            IsActive = true,
            EmailVerified = true,
            SellerProfile = new SellerProfileEntity
            {
                BusinessName = "Data Import",
                IsVerified = true,
                IsActive = true
            }
        };

        _db.Users.Add(seller);
        await _db.SaveChangesAsync(ct);
        return seller;
    }

    private async Task<int> ImportProfilesAsync(string json, CancellationToken ct)
    {
        var profiles = JsonSerializer.Deserialize<List<ProfileImportDto>>(json, JsonOptions);
        if (profiles == null || profiles.Count == 0) return 0;

        var count = 0;
        foreach (var dto in profiles)
        {
            // Check if profile already exists
            if (await _db.PersistentProfiles.AnyAsync(p => p.ProfileKey == dto.ProfileKey, ct))
                continue;

            var profile = new PersistentProfileEntity
            {
                ProfileKey = dto.ProfileKey,
                Interests = dto.Interests ?? new(),
                Affinities = dto.Affinities ?? new(),
                BrandAffinities = dto.BrandAffinities ?? new(),
                Traits = dto.Traits ?? new(),
                TotalSessions = dto.TotalSessions,
                TotalSignals = dto.TotalSignals,
                TotalPurchases = dto.TotalPurchases,
                TotalCartAdds = dto.TotalCartAdds
            };

            _db.PersistentProfiles.Add(profile);
            count++;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<int> ImportCategoriesAsync(string json, CancellationToken ct)
    {
        var categories = JsonSerializer.Deserialize<List<CategoryImportDto>>(json, JsonOptions);
        if (categories == null || categories.Count == 0) return 0;

        var count = 0;
        foreach (var dto in categories)
        {
            if (await _db.Categories.AnyAsync(c => c.Slug == dto.Slug, ct))
                continue;

            var category = new CategoryEntity
            {
                DisplayName = dto.Name,
                Slug = dto.Slug,
                Description = dto.Description
            };

            _db.Categories.Add(category);
            count++;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }

    private async Task<int> ImportDemoUsersAsync(string json, CancellationToken ct)
    {
        var demoUsers = JsonSerializer.Deserialize<List<DemoUserImportDto>>(json, JsonOptions);
        if (demoUsers == null || demoUsers.Count == 0) return 0;

        var count = 0;
        foreach (var dto in demoUsers)
        {
            // Replace existing demo users
            var existing = await _db.DemoUsers.FirstOrDefaultAsync(d => d.Name == dto.Name, ct);
            if (existing != null)
                _db.DemoUsers.Remove(existing);

            var demoUser = new DemoUserEntity
            {
                Id = Guid.NewGuid().ToString("N")[..16],
                Name = dto.Name,
                Persona = dto.Persona ?? "Shopper",
                Description = dto.Description,
                Interests = dto.Interests ?? new(),
                BrandAffinities = dto.BrandAffinities ?? new(),
                PreferredTags = dto.PreferredTags?.ToList() ?? [],
                PriceMin = dto.MinPrice,
                PriceMax = dto.MaxPrice,
                SortOrder = count
            };

            _db.DemoUsers.Add(demoUser);
            count++;
        }

        await _db.SaveChangesAsync(ct);
        return count;
    }
}

public class ImportResult
{
    public bool Success { get; set; }
    public int ProductsImported { get; set; }
    public int ProfilesImported { get; set; }
    public int CategoriesImported { get; set; }
    public int DemoUsersImported { get; set; }
    public int SegmentsGenerated { get; set; }
}

public class ProductImportDto
{
    public string Handle { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public decimal? CompareAtPrice { get; set; }
    public string? Category { get; set; }
    public string? Subcategory { get; set; }
    public string[]? Tags { get; set; }
    public string? ImageUrl { get; set; }
    public bool IsFeatured { get; set; }
    public bool IsTrending { get; set; }
}

public class ProfileImportDto
{
    public string ProfileKey { get; set; } = string.Empty;
    public Dictionary<string, double>? Interests { get; set; }
    public Dictionary<string, double>? Affinities { get; set; }
    public Dictionary<string, double>? BrandAffinities { get; set; }
    public Dictionary<string, bool>? Traits { get; set; }
    public int TotalSessions { get; set; }
    public int TotalSignals { get; set; }
    public int TotalPurchases { get; set; }
    public int TotalCartAdds { get; set; }
    public string[]? Segments { get; set; }
}

public class CategoryImportDto
{
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
}

public class DemoUserImportDto
{
    public string Name { get; set; } = string.Empty;
    public string? Persona { get; set; }
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }
    public Dictionary<string, double>? Interests { get; set; }
    public Dictionary<string, double>? BrandAffinities { get; set; }
    public string[]? PreferredTags { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
}
