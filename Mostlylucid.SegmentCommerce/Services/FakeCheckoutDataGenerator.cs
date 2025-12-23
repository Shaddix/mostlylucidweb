using Bogus;

namespace Mostlylucid.SegmentCommerce.Services;

/// <summary>
/// Generates realistic-looking fake checkout data using Bogus.
/// All data is purely for demo purposes and contains no real PII.
/// </summary>
public class FakeCheckoutDataGenerator
{
    private readonly Faker _faker;
    private static readonly string[] CardBrands = ["Visa", "Mastercard", "Amex", "Discover"];
    private static readonly string[] ShippingMethods = ["Standard", "Express", "Next Day", "Economy"];

    public FakeCheckoutDataGenerator(string? locale = null)
    {
        _faker = new Faker(locale ?? "en");
    }

    /// <summary>
    /// Generate complete fake checkout data for a session.
    /// </summary>
    public CheckoutData GenerateCheckoutData(string sessionKey)
    {
        var address = GenerateAddress();
        var hasDifferentBilling = _faker.Random.Bool(0.2f); // 20% have different billing

        return new CheckoutData
        {
            SessionKey = sessionKey,
            Email = _faker.Internet.Email(),
            FirstName = _faker.Name.FirstName(),
            LastName = _faker.Name.LastName(),
            Phone = _faker.Phone.PhoneNumber(),
            ShippingAddress = address,
            BillingSameAsShipping = !hasDifferentBilling,
            BillingAddress = hasDifferentBilling ? GenerateAddress() : null,
            Payment = GeneratePayment(),
            CurrentStep = CheckoutStep.Cart,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Generate a fake address.
    /// </summary>
    public AddressData GenerateAddress()
    {
        var countryCode = _faker.Random.WeightedRandom(
            ["US", "GB", "CA", "AU", "DE", "FR"],
            [0.5f, 0.15f, 0.1f, 0.1f, 0.075f, 0.075f]
        );

        return new AddressData
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
    /// Generate fake payment data (never real card numbers).
    /// </summary>
    public PaymentData GeneratePayment()
    {
        var brand = _faker.PickRandom(CardBrands);
        
        return new PaymentData
        {
            Method = "card",
            CardBrand = brand,
            CardLastFour = _faker.Random.ReplaceNumbers("####"),
            CardExpiry = $"{_faker.Random.Number(1, 12):D2}/{_faker.Random.Number(25, 30)}",
            CardholderName = _faker.Name.FullName().ToUpperInvariant()
        };
    }

    /// <summary>
    /// Generate a fake email address.
    /// </summary>
    public string GenerateEmail() => _faker.Internet.Email();

    /// <summary>
    /// Generate a fake phone number.
    /// </summary>
    public string GeneratePhone() => _faker.Phone.PhoneNumber();

    /// <summary>
    /// Generate a fake full name.
    /// </summary>
    public (string FirstName, string LastName) GenerateName() 
        => (_faker.Name.FirstName(), _faker.Name.LastName());

    /// <summary>
    /// Generate a fake order number.
    /// </summary>
    public string GenerateOrderNumber()
    {
        var year = DateTime.UtcNow.Year;
        var sequence = _faker.Random.Number(100000, 999999);
        return $"ORD-{year}-{sequence}";
    }

    /// <summary>
    /// Generate fake tracking number.
    /// </summary>
    public string GenerateTrackingNumber()
    {
        var prefix = _faker.PickRandom(new[] { "1Z", "94", "92", "420" });
        return prefix + _faker.Random.ReplaceNumbers("####################")[..18];
    }

    /// <summary>
    /// Pick a random shipping method.
    /// </summary>
    public string PickShippingMethod() => _faker.PickRandom(ShippingMethods);

    /// <summary>
    /// Calculate fake shipping cost based on method.
    /// </summary>
    public decimal CalculateShippingCost(string method, decimal subtotal)
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

    /// <summary>
    /// Calculate fake tax amount (simplified).
    /// </summary>
    public decimal CalculateTax(decimal subtotal, string? countryCode)
    {
        var rate = countryCode switch
        {
            "US" => 0.08m,  // ~8% average
            "GB" => 0.20m,  // 20% VAT
            "CA" => 0.13m,  // ~13% HST
            "AU" => 0.10m,  // 10% GST
            "DE" => 0.19m,  // 19% MwSt
            "FR" => 0.20m,  // 20% TVA
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
