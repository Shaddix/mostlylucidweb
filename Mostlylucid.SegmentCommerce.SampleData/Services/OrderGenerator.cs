using Bogus;
using Mostlylucid.SegmentCommerce.SampleData.Models;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

/// <summary>
/// Generates realistic fake orders using Bogus.
/// Creates complete checkout data including fake customer info, addresses, and payment.
/// </summary>
public class OrderGenerator
{
    private readonly Faker _faker;
    private readonly Random _random;
    private int _orderSequence = 100000;

    private static readonly string[] CardBrands = ["Visa", "Mastercard", "Amex", "Discover"];
    private static readonly string[] ShippingMethods = ["Standard", "Express", "Next Day", "Economy"];
    private static readonly string[] PaymentMethods = ["CreditCard", "PayPal", "ApplePay", "GooglePay"];
    private static readonly string[] OrderStatuses = ["Pending", "Confirmed", "Processing", "Shipped", "Delivered", "Completed"];
    private static readonly string[] FulfillmentStatuses = ["Unfulfilled", "PartiallyFulfilled", "Fulfilled"];

    public OrderGenerator(string? locale = null, int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
        _faker = new Faker(locale ?? "en");
        if (seed.HasValue)
        {
            Randomizer.Seed = new Random(seed.Value);
        }
    }

    /// <summary>
    /// Generate orders for a list of customers based on their generated products/signals.
    /// </summary>
    public List<GeneratedOrder> GenerateOrders(
        List<GeneratedCustomer> customers,
        List<GeneratedProduct> products,
        int minOrdersPerCustomer = 0,
        int maxOrdersPerCustomer = 5)
    {
        var orders = new List<GeneratedOrder>();
        var productList = products.ToList();

        foreach (var customer in customers)
        {
            // Determine how many orders this customer has based on their signals
            var purchaseSignals = customer.Signals.Count(s => s.SignalType == "purchase");
            var orderCount = Math.Max(purchaseSignals, _random.Next(minOrdersPerCustomer, maxOrdersPerCustomer + 1));

            for (int i = 0; i < orderCount; i++)
            {
                var order = GenerateOrder(customer, productList);
                orders.Add(order);
            }
        }

        return orders;
    }

    /// <summary>
    /// Generate a single order for a customer.
    /// </summary>
    public GeneratedOrder GenerateOrder(GeneratedCustomer customer, List<GeneratedProduct> products)
    {
        // Pick products the customer is interested in
        var relevantProducts = products
            .Where(p => customer.RecentCategories.Contains(p.Category))
            .ToList();

        if (!relevantProducts.Any())
        {
            relevantProducts = products.OrderBy(_ => _random.Next()).Take(10).ToList();
        }

        // Generate 1-5 items per order
        var itemCount = _random.Next(1, 6);
        var selectedProducts = relevantProducts
            .OrderBy(_ => _random.Next())
            .Take(itemCount)
            .ToList();

        var items = selectedProducts.Select(p => GenerateOrderItem(p)).ToList();
        var subtotal = items.Sum(i => i.LineTotal);

        var checkoutCustomer = GenerateCheckoutCustomer();
        var shippingMethod = _faker.PickRandom(ShippingMethods);
        var shippingCost = CalculateShippingCost(shippingMethod, subtotal);
        var taxAmount = CalculateTax(subtotal, checkoutCustomer.ShippingAddress.CountryCode);
        var discountAmount = subtotal > 100 ? Math.Round(subtotal * 0.1m, 2) : 0; // 10% off orders over $100

        // Random order date in the past 90 days
        var orderDate = DateTime.UtcNow.AddDays(-_random.Next(1, 91));
        var isCompleted = _random.NextDouble() > 0.1; // 90% of orders are completed

        return new GeneratedOrder
        {
            OrderNumber = GenerateOrderNumber(),
            ProfileKey = customer.ProfileKey,
            SessionKey = $"session_{Guid.NewGuid():N}",
            Customer = checkoutCustomer,
            Items = items,
            Subtotal = subtotal,
            ShippingCost = shippingCost,
            TaxAmount = taxAmount,
            DiscountAmount = discountAmount,
            Total = subtotal + shippingCost + taxAmount - discountAmount,
            Currency = "USD",
            PaymentMethod = _faker.PickRandom(PaymentMethods),
            PaymentStatus = isCompleted ? "Captured" : "Pending",
            Status = isCompleted ? _faker.PickRandom(new[] { "Delivered", "Completed" }) : _faker.PickRandom(OrderStatuses),
            FulfillmentStatus = isCompleted ? "Fulfilled" : _faker.PickRandom(FulfillmentStatuses),
            ShippingCountry = checkoutCustomer.ShippingAddress.Country,
            ShippingRegion = checkoutCustomer.ShippingAddress.State,
            ShippingMethod = shippingMethod,
            CreatedAt = orderDate,
            CompletedAt = isCompleted ? orderDate.AddDays(_random.Next(1, 7)) : null
        };
    }

    /// <summary>
    /// Generate a checkout customer with fake data.
    /// </summary>
    public GeneratedCheckoutCustomer GenerateCheckoutCustomer()
    {
        var hasDifferentBilling = _faker.Random.Bool(0.2f);
        var shippingAddress = GenerateAddress();

        return new GeneratedCheckoutCustomer
        {
            Email = _faker.Internet.Email(),
            FirstName = _faker.Name.FirstName(),
            LastName = _faker.Name.LastName(),
            Phone = _faker.Phone.PhoneNumber(),
            ShippingAddress = shippingAddress,
            BillingSameAsShipping = !hasDifferentBilling,
            BillingAddress = hasDifferentBilling ? GenerateAddress() : null,
            Payment = GeneratePayment()
        };
    }

    /// <summary>
    /// Generate a fake address.
    /// </summary>
    public GeneratedAddress GenerateAddress()
    {
        var countryCode = _faker.Random.WeightedRandom(
            ["US", "GB", "CA", "AU", "DE", "FR"],
            [0.5f, 0.15f, 0.1f, 0.1f, 0.075f, 0.075f]
        );

        return new GeneratedAddress
        {
            Line1 = _faker.Address.StreetAddress(),
            Line2 = _faker.Random.Bool(0.3f) ? _faker.Address.SecondaryAddress() : null,
            City = _faker.Address.City(),
            State = _faker.Address.State(),
            PostalCode = _faker.Address.ZipCode(),
            Country = GetCountryName(countryCode),
            CountryCode = countryCode
        };
    }

    /// <summary>
    /// Generate fake payment data.
    /// </summary>
    public GeneratedPayment GeneratePayment()
    {
        var brand = _faker.PickRandom(CardBrands);
        
        return new GeneratedPayment
        {
            Method = "card",
            CardBrand = brand,
            CardLastFour = _faker.Random.ReplaceNumbers("####"),
            CardExpiry = $"{_faker.Random.Number(1, 12):D2}/{_faker.Random.Number(25, 30)}",
            CardholderName = _faker.Name.FullName().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Generate an order item from a product.
    /// </summary>
    public GeneratedOrderItem GenerateOrderItem(GeneratedProduct product)
    {
        var quantity = _random.Next(1, 4);
        var hasDiscount = product.OriginalPrice.HasValue && product.OriginalPrice > product.Price;
        var discountAmount = hasDiscount 
            ? (product.OriginalPrice!.Value - product.Price) * quantity 
            : 0;
        var lineTotal = product.Price * quantity;

        return new GeneratedOrderItem
        {
            ProductId = product.Id,
            ProductName = product.Name,
            ProductImageUrl = product.Images.FirstOrDefault()?.FilePath,
            Color = product.ColourVariants.FirstOrDefault(),
            Size = "Default",
            Quantity = quantity,
            UnitPrice = product.Price,
            OriginalPrice = product.OriginalPrice,
            DiscountAmount = discountAmount,
            LineTotal = lineTotal
        };
    }

    private string GenerateOrderNumber()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = Interlocked.Increment(ref _orderSequence);
        return $"ORD-{year}-{sequence:D6}";
    }

    private decimal CalculateShippingCost(string method, decimal subtotal)
    {
        return method switch
        {
            "Economy" => subtotal > 50 ? 0 : 3.99m,
            "Standard" => subtotal > 75 ? 0 : 5.99m,
            "Express" => 12.99m,
            "Next Day" => 24.99m,
            _ => 5.99m
        };
    }

    private decimal CalculateTax(decimal subtotal, string? countryCode)
    {
        var rate = countryCode switch
        {
            "US" => 0.08m,
            "GB" => 0.20m,
            "CA" => 0.13m,
            "AU" => 0.10m,
            "DE" => 0.19m,
            "FR" => 0.20m,
            _ => 0.10m
        };
        return Math.Round(subtotal * rate, 2);
    }

    private static string GetCountryName(string code) => code switch
    {
        "US" => "United States",
        "GB" => "United Kingdom",
        "CA" => "Canada",
        "AU" => "Australia",
        "DE" => "Germany",
        "FR" => "France",
        _ => code
    };
}
