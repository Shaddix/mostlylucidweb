using Microsoft.Extensions.Logging;
using Mostlylucid.AudienceSegmentation.Demo.Models;
using Mostlylucid.SemanticSearch.Services;

namespace Mostlylucid.AudienceSegmentation.Demo.Services;

/// <summary>
/// Builds and updates customer profiles based on their behavior
/// </summary>
public class CustomerProfileService
{
    private readonly ILogger<CustomerProfileService> _logger;
    private readonly IEmbeddingService _embeddingService;

    public CustomerProfileService(
        ILogger<CustomerProfileService> logger,
        IEmbeddingService embeddingService)
    {
        _logger = logger;
        _embeddingService = embeddingService;
    }

    /// <summary>
    /// Build initial customer profile from their interactions
    /// </summary>
    public async Task<Customer> BuildCustomerProfileAsync(
        Customer customer,
        List<Product> allProducts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Building profile for customer {Id}", customer.Id);

        // Build profile text from customer behavior
        var profileParts = new List<string>();

        // Add search queries (what they're looking for)
        if (customer.SearchQueries.Any())
        {
            profileParts.Add($"Searched for: {string.Join(", ", customer.SearchQueries)}");
        }

        // Add viewed products
        if (customer.ViewedProducts.Any())
        {
            var viewedProductNames = customer.ViewedProducts
                .Select(id => allProducts.FirstOrDefault(p => p.Id == id)?.Name)
                .Where(name => name != null)
                .ToList();

            if (viewedProductNames.Any())
            {
                profileParts.Add($"Viewed products: {string.Join(", ", viewedProductNames)}");
            }
        }

        // Add purchased products (stronger signal)
        if (customer.PurchasedProducts.Any())
        {
            var purchasedProductNames = customer.PurchasedProducts
                .Select(id => allProducts.FirstOrDefault(p => p.Id == id)?.Name)
                .Where(name => name != null)
                .ToList();

            if (purchasedProductNames.Any())
            {
                // Add purchased items multiple times to increase their weight
                profileParts.Add($"Purchased: {string.Join(", ", purchasedProductNames)}. " +
                               $"Purchased: {string.Join(", ", purchasedProductNames)}. " +
                               $"Purchased: {string.Join(", ", purchasedProductNames)}");
            }
        }

        // Add category interests
        if (customer.CategoryInterests.Any())
        {
            var interests = customer.CategoryInterests
                .OrderByDescending(kvp => kvp.Value)
                .Select(kvp => $"{kvp.Key} ({kvp.Value} interactions)")
                .ToList();

            profileParts.Add($"Category interests: {string.Join(", ", interests)}");
        }

        // Generate embedding from profile
        if (profileParts.Any())
        {
            var profileText = string.Join(". ", profileParts);
            customer.ProfileEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                profileText,
                cancellationToken
            );

            _logger.LogDebug("Generated profile embedding for customer {Id}: {Length} dimensions",
                customer.Id, customer.ProfileEmbedding?.Length ?? 0);
        }
        else
        {
            _logger.LogWarning("Customer {Id} has no interaction data for profile building",
                customer.Id);

            // Create default embedding
            customer.ProfileEmbedding = await _embeddingService.GenerateEmbeddingAsync(
                "New customer with no preferences yet",
                cancellationToken
            );
        }

        return customer;
    }

    /// <summary>
    /// Update customer profile after new interaction
    /// </summary>
    public async Task<Customer> UpdateProfileAfterInteractionAsync(
        Customer customer,
        string interactionType,
        Product product,
        List<Product> allProducts,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Updating customer {Id} profile after {Interaction} with {Product}",
            customer.Id, interactionType, product.Name);

        // Update interaction data
        switch (interactionType.ToLower())
        {
            case "view":
                if (!customer.ViewedProducts.Contains(product.Id))
                {
                    customer.ViewedProducts.Add(product.Id);
                }
                customer.CategoryInterests[product.Category] =
                    customer.CategoryInterests.GetValueOrDefault(product.Category) + 1;
                break;

            case "purchase":
                if (!customer.PurchasedProducts.Contains(product.Id))
                {
                    customer.PurchasedProducts.Add(product.Id);
                }
                customer.CategoryInterests[product.Category] =
                    customer.CategoryInterests.GetValueOrDefault(product.Category) + 5; // Purchases weighted higher
                break;

            case "search":
                customer.SearchQueries.Add(product.Name);
                break;
        }

        customer.LastActivity = DateTime.UtcNow;

        // Rebuild profile embedding
        return await BuildCustomerProfileAsync(customer, allProducts, cancellationToken);
    }

    /// <summary>
    /// Generate a synthetic customer with realistic behavior
    /// </summary>
    public async Task<Customer> GenerateSyntheticCustomerAsync(
        string customerId,
        List<Product> allProducts,
        CancellationToken cancellationToken = default)
    {
        var random = new Random();
        var customerTypes = new[]
        {
            ("Budget Hunter", new[] { "Budget", "Sale", "Affordable", "Discount" }),
            ("Tech Enthusiast", new[] { "Electronics", "Tech", "Innovation", "Smart" }),
            ("Luxury Seeker", new[] { "Luxury", "Premium", "Designer", "Exclusive" }),
            ("Eco Warrior", new[] { "Eco", "Sustainable", "Organic", "Green" }),
            ("Fitness Fan", new[] { "Sports", "Fitness", "Health", "Active" }),
            ("Homebody", new[] { "Home", "Comfort", "Cozy", "Indoor" }),
            ("Fashionista", new[] { "Fashion", "Style", "Trendy", "Chic" }),
            ("Foodie", new[] { "Food", "Gourmet", "Culinary", "Kitchen" })
        };

        var (customerType, interests) = customerTypes[random.Next(customerTypes.Length)];

        var customer = new Customer
        {
            Id = customerId,
            Name = $"{customerType} Customer #{customerId[..8]}"
        };

        // Generate realistic search queries based on customer type
        customer.SearchQueries.AddRange(
            interests
                .OrderBy(_ => random.Next())
                .Take(random.Next(2, 5))
                .ToList()
        );

        // Find products matching interests
        var matchingProducts = allProducts
            .Where(p =>
                interests.Any(interest =>
                    p.Name.Contains(interest, StringComparison.OrdinalIgnoreCase) ||
                    p.Description.Contains(interest, StringComparison.OrdinalIgnoreCase) ||
                    p.Tags.Any(t => t.Contains(interest, StringComparison.OrdinalIgnoreCase)) ||
                    p.Category.Contains(interest, StringComparison.OrdinalIgnoreCase)
                )
            )
            .ToList();

        // Simulate viewing products (3-8 products)
        var viewCount = random.Next(3, Math.Min(9, matchingProducts.Count + 1));
        var viewedProducts = matchingProducts
            .OrderBy(_ => random.Next())
            .Take(viewCount)
            .ToList();

        foreach (var product in viewedProducts)
        {
            customer.ViewedProducts.Add(product.Id);
            customer.CategoryInterests[product.Category] =
                customer.CategoryInterests.GetValueOrDefault(product.Category) + 1;
        }

        // Simulate purchases (0-3 products from viewed)
        var purchaseCount = random.Next(0, Math.Min(4, viewedProducts.Count + 1));
        var purchasedProducts = viewedProducts
            .OrderBy(_ => random.Next())
            .Take(purchaseCount)
            .ToList();

        foreach (var product in purchasedProducts)
        {
            customer.PurchasedProducts.Add(product.Id);
            customer.CategoryInterests[product.Category] =
                customer.CategoryInterests.GetValueOrDefault(product.Category) + 5;
        }

        customer.LastActivity = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440)); // Within last 24 hours

        // Build profile
        await BuildCustomerProfileAsync(customer, allProducts, cancellationToken);

        _logger.LogInformation("Generated synthetic customer: {Type} with {Views} views, {Purchases} purchases",
            customerType, viewCount, purchaseCount);

        return customer;
    }
}
