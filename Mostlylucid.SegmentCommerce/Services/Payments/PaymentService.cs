using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Events;

namespace Mostlylucid.SegmentCommerce.Services.Payments;

/// <summary>
/// Result of a payment operation.
/// </summary>
public record PaymentResult(
    bool Success,
    string? PaymentId = null,
    string? Error = null,
    PaymentStatus Status = PaymentStatus.Pending)
{
    public static PaymentResult Ok(string paymentId, PaymentStatus status = PaymentStatus.Authorized) 
        => new(true, paymentId, null, status);
    public static PaymentResult Fail(string error) 
        => new(false, null, error, PaymentStatus.Failed);
}

/// <summary>
/// Payment request for authorization.
/// </summary>
public record PaymentRequest(
    int OrderId,
    decimal Amount,
    string Currency,
    string? PaymentMethodId = null,
    PaymentMethodType MethodType = PaymentMethodType.Card,
    string? CardToken = null,
    string? CustomerEmail = null,
    string? Description = null,
    Dictionary<string, string>? Metadata = null);

/// <summary>
/// Refund request.
/// </summary>
public record RefundRequest(
    string PaymentId,
    decimal? Amount = null,  // null = full refund
    string? Reason = null);

public enum PaymentMethodType
{
    Card,
    PayPal,
    ApplePay,
    GooglePay,
    BankTransfer
}

/// <summary>
/// Payment service abstraction for processing payments.
/// Implementations can use Stripe, PayPal, or mock for testing.
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Authorize a payment (hold funds without capturing).
    /// </summary>
    Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Capture a previously authorized payment.
    /// </summary>
    Task<PaymentResult> CaptureAsync(string paymentId, decimal? amount = null, CancellationToken ct = default);

    /// <summary>
    /// Authorize and capture in one step.
    /// </summary>
    Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default);

    /// <summary>
    /// Refund a captured payment.
    /// </summary>
    Task<PaymentResult> RefundAsync(RefundRequest request, CancellationToken ct = default);

    /// <summary>
    /// Cancel/void an authorized payment.
    /// </summary>
    Task<PaymentResult> CancelAsync(string paymentId, CancellationToken ct = default);

    /// <summary>
    /// Get payment status.
    /// </summary>
    Task<PaymentResult> GetStatusAsync(string paymentId, CancellationToken ct = default);
}

/// <summary>
/// Mock payment service for testing and demo purposes.
/// Simulates payment processing with configurable behavior.
/// </summary>
public class MockPaymentService : IPaymentService
{
    private readonly IEventPublisher _events;
    private readonly ILogger<MockPaymentService> _logger;
    private readonly MockPaymentConfig _config;

    // In-memory store for payment state
    private static readonly Dictionary<string, MockPayment> _payments = new();
    private static readonly object _lock = new();

    public MockPaymentService(
        IEventPublisher events,
        ILogger<MockPaymentService> logger,
        IConfiguration configuration)
    {
        _events = events;
        _logger = logger;
        _config = configuration.GetSection("Payments:Mock").Get<MockPaymentConfig>() ?? new();
    }

    public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        // Simulate processing delay
        SimulateDelay();

        // Check for simulated failures
        if (ShouldFail(request.Amount))
        {
            _logger.LogWarning("Mock payment authorization failed for order {OrderId}", request.OrderId);
            
            _events.Publish(new PaymentFailedEvent(
                request.OrderId,
                null,
                "Payment declined - insufficient funds (simulated)"));
            
            return Task.FromResult(PaymentResult.Fail("Payment declined - insufficient funds"));
        }

        var paymentId = $"pay_{Guid.NewGuid():N}";
        var payment = new MockPayment
        {
            PaymentId = paymentId,
            OrderId = request.OrderId,
            Amount = request.Amount,
            Currency = request.Currency,
            Status = PaymentStatus.Authorized,
            CreatedAt = DateTime.UtcNow
        };

        lock (_lock)
        {
            _payments[paymentId] = payment;
        }

        _events.Publish(new PaymentAuthorizedEvent(
            request.OrderId,
            paymentId,
            request.Amount,
            request.Currency));

        _logger.LogInformation("Mock payment authorized: {PaymentId} for {Amount} {Currency}",
            paymentId, request.Amount, request.Currency);

        return Task.FromResult(PaymentResult.Ok(paymentId, PaymentStatus.Authorized));
    }

    public Task<PaymentResult> CaptureAsync(string paymentId, decimal? amount = null, CancellationToken ct = default)
    {
        SimulateDelay();

        lock (_lock)
        {
            if (!_payments.TryGetValue(paymentId, out var payment))
            {
                return Task.FromResult(PaymentResult.Fail($"Payment {paymentId} not found"));
            }

            if (payment.Status != PaymentStatus.Authorized)
            {
                return Task.FromResult(PaymentResult.Fail($"Payment {paymentId} is not in authorized state"));
            }

            var captureAmount = amount ?? payment.Amount;
            if (captureAmount > payment.Amount)
            {
                return Task.FromResult(PaymentResult.Fail("Capture amount exceeds authorized amount"));
            }

            payment.Status = PaymentStatus.Captured;
            payment.CapturedAmount = captureAmount;
            payment.CapturedAt = DateTime.UtcNow;

            _events.Publish(new PaymentCapturedEvent(
                payment.OrderId,
                paymentId,
                captureAmount));

            _logger.LogInformation("Mock payment captured: {PaymentId} for {Amount}",
                paymentId, captureAmount);

            return Task.FromResult(PaymentResult.Ok(paymentId, PaymentStatus.Captured));
        }
    }

    public async Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        var authResult = await AuthorizeAsync(request, ct);
        if (!authResult.Success)
        {
            return authResult;
        }

        return await CaptureAsync(authResult.PaymentId!, request.Amount, ct);
    }

    public Task<PaymentResult> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        SimulateDelay();

        lock (_lock)
        {
            if (!_payments.TryGetValue(request.PaymentId, out var payment))
            {
                return Task.FromResult(PaymentResult.Fail($"Payment {request.PaymentId} not found"));
            }

            if (payment.Status != PaymentStatus.Captured)
            {
                return Task.FromResult(PaymentResult.Fail($"Payment {request.PaymentId} has not been captured"));
            }

            var refundAmount = request.Amount ?? payment.CapturedAmount ?? payment.Amount;
            
            if (refundAmount > (payment.CapturedAmount ?? payment.Amount) - (payment.RefundedAmount ?? 0))
            {
                return Task.FromResult(PaymentResult.Fail("Refund amount exceeds captured amount"));
            }

            payment.RefundedAmount = (payment.RefundedAmount ?? 0) + refundAmount;
            
            if (payment.RefundedAmount >= payment.CapturedAmount)
            {
                payment.Status = PaymentStatus.Refunded;
            }
            else
            {
                payment.Status = PaymentStatus.PartiallyRefunded;
            }

            _events.Publish(new PaymentRefundedEvent(
                payment.OrderId,
                request.PaymentId,
                refundAmount));

            _logger.LogInformation("Mock payment refunded: {PaymentId} for {Amount}",
                request.PaymentId, refundAmount);

            return Task.FromResult(PaymentResult.Ok(request.PaymentId, payment.Status));
        }
    }

    public Task<PaymentResult> CancelAsync(string paymentId, CancellationToken ct = default)
    {
        SimulateDelay();

        lock (_lock)
        {
            if (!_payments.TryGetValue(paymentId, out var payment))
            {
                return Task.FromResult(PaymentResult.Fail($"Payment {paymentId} not found"));
            }

            if (payment.Status != PaymentStatus.Authorized)
            {
                return Task.FromResult(PaymentResult.Fail(
                    $"Cannot cancel payment in {payment.Status} state"));
            }

            payment.Status = PaymentStatus.Cancelled;

            _logger.LogInformation("Mock payment cancelled: {PaymentId}", paymentId);

            return Task.FromResult(PaymentResult.Ok(paymentId, PaymentStatus.Cancelled));
        }
    }

    public Task<PaymentResult> GetStatusAsync(string paymentId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (!_payments.TryGetValue(paymentId, out var payment))
            {
                return Task.FromResult(PaymentResult.Fail($"Payment {paymentId} not found"));
            }

            return Task.FromResult(PaymentResult.Ok(paymentId, payment.Status));
        }
    }

    private void SimulateDelay()
    {
        if (_config.SimulateDelayMs > 0)
        {
            Thread.Sleep(_config.SimulateDelayMs);
        }
    }

    private bool ShouldFail(decimal amount)
    {
        // Specific amounts trigger failures for testing
        if (_config.FailOnAmounts?.Contains(amount) == true)
        {
            return true;
        }

        // Random failure rate
        if (_config.FailureRate > 0 && Random.Shared.NextDouble() < _config.FailureRate)
        {
            return true;
        }

        return false;
    }

    private class MockPayment
    {
        public string PaymentId { get; set; } = string.Empty;
        public int OrderId { get; set; }
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "USD";
        public PaymentStatus Status { get; set; }
        public decimal? CapturedAmount { get; set; }
        public decimal? RefundedAmount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CapturedAt { get; set; }
    }
}

public class MockPaymentConfig
{
    /// <summary>
    /// Simulated processing delay in milliseconds.
    /// </summary>
    public int SimulateDelayMs { get; set; } = 100;

    /// <summary>
    /// Random failure rate (0.0 to 1.0).
    /// </summary>
    public double FailureRate { get; set; } = 0.0;

    /// <summary>
    /// Specific amounts that always fail (for testing).
    /// E.g., [99.99] means $99.99 always fails.
    /// </summary>
    public List<decimal>? FailOnAmounts { get; set; }
}

/// <summary>
/// Stripe payment service implementation.
/// Requires Stripe.net NuGet package and API keys.
/// </summary>
public class StripePaymentService : IPaymentService
{
    private readonly IEventPublisher _events;
    private readonly ILogger<StripePaymentService> _logger;
    private readonly string _secretKey;

    public StripePaymentService(
        IEventPublisher events,
        ILogger<StripePaymentService> logger,
        IConfiguration configuration)
    {
        _events = events;
        _logger = logger;
        _secretKey = configuration["Payments:Stripe:SecretKey"] 
            ?? throw new InvalidOperationException("Stripe SecretKey not configured");
        
        // In real implementation: Stripe.StripeConfiguration.ApiKey = _secretKey;
    }

    public Task<PaymentResult> AuthorizeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        // Real implementation would use Stripe SDK:
        // var options = new PaymentIntentCreateOptions
        // {
        //     Amount = (long)(request.Amount * 100),
        //     Currency = request.Currency.ToLower(),
        //     CaptureMethod = "manual",
        //     PaymentMethod = request.PaymentMethodId,
        //     Confirm = true
        // };
        // var intent = await _paymentIntentService.CreateAsync(options, cancellationToken: ct);
        
        _logger.LogWarning("StripePaymentService.AuthorizeAsync called but Stripe SDK not installed");
        return Task.FromResult(PaymentResult.Fail("Stripe SDK not installed. Add Stripe.net NuGet package."));
    }

    public Task<PaymentResult> CaptureAsync(string paymentId, decimal? amount = null, CancellationToken ct = default)
    {
        _logger.LogWarning("StripePaymentService.CaptureAsync called but Stripe SDK not installed");
        return Task.FromResult(PaymentResult.Fail("Stripe SDK not installed"));
    }

    public Task<PaymentResult> ChargeAsync(PaymentRequest request, CancellationToken ct = default)
    {
        _logger.LogWarning("StripePaymentService.ChargeAsync called but Stripe SDK not installed");
        return Task.FromResult(PaymentResult.Fail("Stripe SDK not installed"));
    }

    public Task<PaymentResult> RefundAsync(RefundRequest request, CancellationToken ct = default)
    {
        _logger.LogWarning("StripePaymentService.RefundAsync called but Stripe SDK not installed");
        return Task.FromResult(PaymentResult.Fail("Stripe SDK not installed"));
    }

    public Task<PaymentResult> CancelAsync(string paymentId, CancellationToken ct = default)
    {
        _logger.LogWarning("StripePaymentService.CancelAsync called but Stripe SDK not installed");
        return Task.FromResult(PaymentResult.Fail("Stripe SDK not installed"));
    }

    public Task<PaymentResult> GetStatusAsync(string paymentId, CancellationToken ct = default)
    {
        _logger.LogWarning("StripePaymentService.GetStatusAsync called but Stripe SDK not installed");
        return Task.FromResult(PaymentResult.Fail("Stripe SDK not installed"));
    }
}
