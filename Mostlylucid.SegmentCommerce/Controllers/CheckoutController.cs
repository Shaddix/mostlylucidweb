using Microsoft.AspNetCore.Mvc;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services;
using Mostlylucid.SegmentCommerce.Services.Events;
using Mostlylucid.SegmentCommerce.Services.Inventory;
using Mostlylucid.SegmentCommerce.Services.Orders;
using Mostlylucid.SegmentCommerce.Services.Payments;

namespace Mostlylucid.SegmentCommerce.Controllers;

/// <summary>
/// View model for checkout pages.
/// </summary>
public class CheckoutViewModel
{
    public Cart Cart { get; set; } = new();
    public CheckoutStep CurrentStep { get; set; } = CheckoutStep.Cart;
    public List<StockAvailability>? StockIssues { get; set; }
    public string? Error { get; set; }
    
    // Shipping info
    public string? ShippingCountry { get; set; }
    public string? ShippingRegion { get; set; }
    public string? ShippingMethod { get; set; }
    public decimal ShippingCost { get; set; }
    
    // Order summary
    public decimal Subtotal => Cart.Subtotal;
    public decimal Tax { get; set; }
    public decimal Total => Subtotal + ShippingCost + Tax;
}

public enum CheckoutStep
{
    Cart,
    Shipping,
    Payment,
    Confirmation
}

/// <summary>
/// Input for placing an order.
/// </summary>
public class PlaceOrderRequest
{
    public string? ShippingCountry { get; set; }
    public string? ShippingRegion { get; set; }
    public string? ShippingMethod { get; set; }
    public string? PaymentMethodId { get; set; }
    public string? CouponCode { get; set; }
}

/// <summary>
/// Order confirmation view model.
/// </summary>
public class OrderConfirmationViewModel
{
    public OrderEntity Order { get; set; } = null!;
    public string? PaymentId { get; set; }
}

public class CheckoutController : Controller
{
    private readonly ICartService _cartService;
    private readonly IOrderService _orderService;
    private readonly IInventoryService _inventoryService;
    private readonly IPaymentService _paymentService;
    private readonly ISessionService _sessionService;
    private readonly IEventPublisher _events;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICartService cartService,
        IOrderService orderService,
        IInventoryService inventoryService,
        IPaymentService paymentService,
        ISessionService sessionService,
        IEventPublisher events,
        ILogger<CheckoutController> logger)
    {
        _cartService = cartService;
        _orderService = orderService;
        _inventoryService = inventoryService;
        _paymentService = paymentService;
        _sessionService = sessionService;
        _events = events;
        _logger = logger;
    }

    /// <summary>
    /// GET /Checkout - Show checkout page with cart review.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var cart = await _cartService.GetCartAsync();
        
        if (cart.Items.Count == 0)
        {
            return RedirectToAction("Index", "Cart");
        }

        var viewModel = new CheckoutViewModel
        {
            Cart = cart,
            CurrentStep = CheckoutStep.Cart
        };

        // Check stock availability
        var stockCheck = await CheckStockAsync(cart);
        if (stockCheck.Any(s => !s.IsAvailable))
        {
            viewModel.StockIssues = stockCheck.Where(s => !s.IsAvailable).ToList();
            viewModel.Error = "Some items in your cart are out of stock or have limited availability.";
        }

        return View(viewModel);
    }

    /// <summary>
    /// GET /Checkout/Shipping - Shipping information step.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Shipping()
    {
        var cart = await _cartService.GetCartAsync();
        
        if (cart.Items.Count == 0)
        {
            return RedirectToAction("Index", "Cart");
        }

        var viewModel = new CheckoutViewModel
        {
            Cart = cart,
            CurrentStep = CheckoutStep.Shipping
        };

        return View(viewModel);
    }

    /// <summary>
    /// POST /Checkout/Shipping - Save shipping info and continue.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Shipping(CheckoutViewModel model)
    {
        var cart = await _cartService.GetCartAsync();
        
        if (cart.Items.Count == 0)
        {
            return RedirectToAction("Index", "Cart");
        }

        // Store shipping info in session
        HttpContext.Session.SetString("Checkout_ShippingCountry", model.ShippingCountry ?? "");
        HttpContext.Session.SetString("Checkout_ShippingRegion", model.ShippingRegion ?? "");
        HttpContext.Session.SetString("Checkout_ShippingMethod", model.ShippingMethod ?? "standard");

        return RedirectToAction(nameof(Payment));
    }

    /// <summary>
    /// GET /Checkout/Payment - Payment step.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Payment()
    {
        var cart = await _cartService.GetCartAsync();
        
        if (cart.Items.Count == 0)
        {
            return RedirectToAction("Index", "Cart");
        }

        var viewModel = new CheckoutViewModel
        {
            Cart = cart,
            CurrentStep = CheckoutStep.Payment,
            ShippingCountry = HttpContext.Session.GetString("Checkout_ShippingCountry"),
            ShippingRegion = HttpContext.Session.GetString("Checkout_ShippingRegion"),
            ShippingMethod = HttpContext.Session.GetString("Checkout_ShippingMethod"),
            ShippingCost = CalculateShipping(HttpContext.Session.GetString("Checkout_ShippingMethod")),
            Tax = CalculateTax(cart.Subtotal, HttpContext.Session.GetString("Checkout_ShippingRegion"))
        };

        return View(viewModel);
    }

    /// <summary>
    /// POST /Checkout/PlaceOrder - Process the order.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(PlaceOrderRequest request)
    {
        var cart = await _cartService.GetCartAsync();
        
        if (cart.Items.Count == 0)
        {
            return RedirectToAction("Index", "Cart");
        }

        // Validate stock one more time
        var stockCheck = await CheckStockAsync(cart);
        var outOfStock = stockCheck.Where(s => !s.IsAvailable).ToList();
        
        if (outOfStock.Any())
        {
            TempData["Error"] = "Some items are no longer available. Please review your cart.";
            return RedirectToAction(nameof(Index));
        }

        // Get profile/session info
        var profileId = HttpContext.Items["ProfileId"] as Guid?;
        var sessionKey = _sessionService.GetSessionId();

        // Create order request
        var orderRequest = new CreateOrderRequest(
            ProfileId: profileId,
            SessionKey: sessionKey,
            Items: cart.Items.Select(i => new OrderItemRequest(
                i.Product.Id,
                i.VariationId,
                i.Quantity
            )).ToList(),
            ShippingCountry: request.ShippingCountry ?? HttpContext.Session.GetString("Checkout_ShippingCountry"),
            ShippingRegion: request.ShippingRegion ?? HttpContext.Session.GetString("Checkout_ShippingRegion"),
            ShippingMethod: request.ShippingMethod ?? HttpContext.Session.GetString("Checkout_ShippingMethod"),
            Metadata: string.IsNullOrEmpty(request.CouponCode) ? null : new OrderMetadata
            {
                CouponCode = request.CouponCode
            }
        );

        // Create the order
        var orderResult = await _orderService.CreateOrderAsync(orderRequest);
        
        if (!orderResult.Success)
        {
            _logger.LogWarning("Order creation failed: {Error}", orderResult.Error);
            TempData["Error"] = orderResult.Error;
            return RedirectToAction(nameof(Payment));
        }

        var order = orderResult.Order!;

        // Process payment
        var paymentRequest = new PaymentRequest(
            OrderId: order.Id,
            Amount: order.Total,
            Currency: order.Currency,
            PaymentMethodId: request.PaymentMethodId,
            Description: $"Order {order.OrderNumber}"
        );

        var paymentResult = await _paymentService.ChargeAsync(paymentRequest);

        if (!paymentResult.Success)
        {
            _logger.LogWarning("Payment failed for order {OrderNumber}: {Error}",
                order.OrderNumber, paymentResult.Error);
            
            // Cancel the order since payment failed
            await _orderService.CancelOrderAsync(order.Id, $"Payment failed: {paymentResult.Error}");
            
            TempData["Error"] = $"Payment failed: {paymentResult.Error}";
            return RedirectToAction(nameof(Payment));
        }

        // Update order payment status
        order.PaymentStatus = paymentResult.Status;
        
        // Confirm the order
        await _orderService.ConfirmOrderAsync(order.Id);

        // Clear cart
        _cartService.ClearCart();
        
        // Clear checkout session data
        HttpContext.Session.Remove("Checkout_ShippingCountry");
        HttpContext.Session.Remove("Checkout_ShippingRegion");
        HttpContext.Session.Remove("Checkout_ShippingMethod");

        _logger.LogInformation("Order {OrderNumber} placed successfully with payment {PaymentId}",
            order.OrderNumber, paymentResult.PaymentId);

        return RedirectToAction(nameof(Confirmation), new { orderNumber = order.OrderNumber });
    }

    /// <summary>
    /// GET /Checkout/Confirmation/{orderNumber} - Order confirmation page.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Confirmation(string orderNumber)
    {
        var order = await _orderService.GetByOrderNumberAsync(orderNumber);
        
        if (order == null)
        {
            return NotFound();
        }

        // Security: Only show order to the session that created it
        var sessionKey = _sessionService.GetSessionId();
        var profileId = HttpContext.Items["ProfileId"] as Guid?;
        
        if (order.SessionKey != sessionKey && order.ProfileId != profileId)
        {
            return NotFound();
        }

        var viewModel = new OrderConfirmationViewModel
        {
            Order = order
        };

        return View(viewModel);
    }

    /// <summary>
    /// GET /Checkout/Orders - Order history.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Orders()
    {
        var profileId = HttpContext.Items["ProfileId"] as Guid?;
        
        if (!profileId.HasValue)
        {
            return View(new List<OrderEntity>());
        }

        var orders = await _orderService.GetByProfileAsync(profileId.Value, limit: 20);
        return View(orders);
    }

    // ============ API ENDPOINTS FOR HTMX ============

    /// <summary>
    /// POST /api/checkout/validate-stock - Validate cart stock via HTMX.
    /// </summary>
    [HttpPost("/api/checkout/validate-stock")]
    public async Task<IActionResult> ValidateStock()
    {
        var cart = await _cartService.GetCartAsync();
        var stockCheck = await CheckStockAsync(cart);
        var issues = stockCheck.Where(s => !s.IsAvailable).ToList();

        return Json(new
        {
            valid = !issues.Any(),
            issues = issues.Select(i => new
            {
                productId = i.ProductId,
                variationId = i.VariationId,
                requested = i.RequestedQuantity,
                available = i.AvailableQuantity,
                shortage = i.Shortage
            })
        });
    }

    /// <summary>
    /// POST /api/checkout/calculate-totals - Calculate shipping/tax via HTMX.
    /// </summary>
    [HttpPost("/api/checkout/calculate-totals")]
    public async Task<IActionResult> CalculateTotals([FromBody] ShippingCalculationRequest request)
    {
        var cart = await _cartService.GetCartAsync();
        var shipping = CalculateShipping(request.ShippingMethod);
        var tax = CalculateTax(cart.Subtotal, request.ShippingRegion);

        return Json(new
        {
            subtotal = cart.Subtotal,
            shipping,
            tax,
            total = cart.Subtotal + shipping + tax
        });
    }

    // ============ PRIVATE HELPERS ============

    private async Task<List<StockAvailability>> CheckStockAsync(Cart cart)
    {
        var items = cart.Items.Select(i => (i.Product.Id, i.VariationId, i.Quantity)).ToList();
        return await _inventoryService.CheckAvailabilityAsync(items);
    }

    private static decimal CalculateShipping(string? method)
    {
        return method?.ToLowerInvariant() switch
        {
            "express" => 14.99m,
            "overnight" => 29.99m,
            "free" => 0m,
            _ => 5.99m // standard
        };
    }

    private static decimal CalculateTax(decimal subtotal, string? region)
    {
        // Simplified tax calculation - in production use a tax service
        var rate = region?.ToUpperInvariant() switch
        {
            "CA" => 0.0725m,  // California
            "NY" => 0.08m,    // New York
            "TX" => 0.0625m,  // Texas
            "WA" => 0.065m,   // Washington
            "FL" => 0.06m,    // Florida
            _ => 0.05m        // Default
        };

        return Math.Round(subtotal * rate, 2);
    }
}

public class ShippingCalculationRequest
{
    public string? ShippingMethod { get; set; }
    public string? ShippingRegion { get; set; }
}
