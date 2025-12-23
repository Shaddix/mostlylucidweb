using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Models;
using Mostlylucid.SegmentCommerce.Services.Events;
using Mostlylucid.SegmentCommerce.Services.Inventory;

namespace Mostlylucid.SegmentCommerce.Services.Orders;

/// <summary>
/// Result of an order operation.
/// </summary>
public record OrderResult(bool Success, string? Error = null, OrderEntity? Order = null)
{
    public static OrderResult Ok(OrderEntity order) => new(true, null, order);
    public static OrderResult Fail(string error) => new(false, error);
}

/// <summary>
/// Input for creating a new order.
/// </summary>
public record CreateOrderRequest(
    Guid? ProfileId,
    string? SessionKey,
    List<OrderItemRequest> Items,
    string? ShippingCountry = null,
    string? ShippingRegion = null,
    string? ShippingMethod = null,
    OrderMetadata? Metadata = null);

public record OrderItemRequest(
    int ProductId,
    int? VariationId,
    int Quantity);

/// <summary>
/// Valid order state transitions.
/// </summary>
public static class OrderStateTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> ValidTransitions = new()
    {
        [OrderStatus.Pending] = [OrderStatus.Confirmed, OrderStatus.Cancelled],
        [OrderStatus.Confirmed] = [OrderStatus.Processing, OrderStatus.Cancelled],
        [OrderStatus.Processing] = [OrderStatus.Shipped, OrderStatus.Cancelled],
        [OrderStatus.Shipped] = [OrderStatus.Delivered, OrderStatus.Cancelled],
        [OrderStatus.Delivered] = [OrderStatus.Completed, OrderStatus.Refunded],
        [OrderStatus.Completed] = [OrderStatus.Refunded],
        [OrderStatus.Cancelled] = [],
        [OrderStatus.Refunded] = []
    };

    public static bool CanTransition(OrderStatus from, OrderStatus to)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    public static OrderStatus[] GetAllowedTransitions(OrderStatus from)
    {
        return ValidTransitions.TryGetValue(from, out var allowed) ? allowed : [];
    }
}

/// <summary>
/// Service for managing orders with proper state machine and validation.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Create a new order from the given items.
    /// </summary>
    Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default);

    /// <summary>
    /// Get an order by ID.
    /// </summary>
    Task<OrderEntity?> GetByIdAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Get an order by order number.
    /// </summary>
    Task<OrderEntity?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default);

    /// <summary>
    /// Get orders for a profile.
    /// </summary>
    Task<List<OrderEntity>> GetByProfileAsync(Guid profileId, int limit = 10, CancellationToken ct = default);

    /// <summary>
    /// Transition order to a new status with validation.
    /// </summary>
    Task<OrderResult> TransitionStatusAsync(int orderId, OrderStatus newStatus, string? reason = null, CancellationToken ct = default);

    /// <summary>
    /// Confirm an order (from Pending to Confirmed).
    /// </summary>
    Task<OrderResult> ConfirmOrderAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Ship an order with optional tracking number.
    /// </summary>
    Task<OrderResult> ShipOrderAsync(int orderId, string? trackingNumber = null, CancellationToken ct = default);

    /// <summary>
    /// Mark order as delivered.
    /// </summary>
    Task<OrderResult> DeliverOrderAsync(int orderId, CancellationToken ct = default);

    /// <summary>
    /// Cancel an order.
    /// </summary>
    Task<OrderResult> CancelOrderAsync(int orderId, string reason, CancellationToken ct = default);

    /// <summary>
    /// Refund an order.
    /// </summary>
    Task<OrderResult> RefundOrderAsync(int orderId, decimal? partialAmount = null, CancellationToken ct = default);
}

public class OrderService : IOrderService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly IInventoryService _inventory;
    private readonly IEventPublisher _events;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        SegmentCommerceDbContext db,
        IInventoryService inventory,
        IEventPublisher events,
        ILogger<OrderService> logger)
    {
        _db = db;
        _inventory = inventory;
        _events = events;
        _logger = logger;
    }

    public async Task<OrderResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct = default)
    {
        if (request.Items.Count == 0)
        {
            return OrderResult.Fail("Order must have at least one item");
        }

        // Validate stock availability
        var stockItems = request.Items
            .Select(i => (i.ProductId, i.VariationId, i.Quantity))
            .ToList();

        var availability = await _inventory.CheckAvailabilityAsync(stockItems, ct);
        var unavailable = availability.Where(a => !a.IsAvailable).ToList();

        if (unavailable.Any())
        {
            var errors = string.Join(", ", unavailable.Select(u =>
                $"Product {u.ProductId}: need {u.RequestedQuantity}, have {u.AvailableQuantity}"));
            return OrderResult.Fail($"Insufficient stock: {errors}");
        }

        // Load products for pricing
        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Variations)
            .ToDictionaryAsync(p => p.Id, ct);

        // Create order
        var orderNumber = GenerateOrderNumber();
        var order = new OrderEntity
        {
            OrderNumber = orderNumber,
            ProfileId = request.ProfileId,
            SessionKey = request.SessionKey,
            Status = OrderStatus.Pending,
            PaymentStatus = PaymentStatus.Pending,
            FulfillmentStatus = FulfillmentStatus.Unfulfilled,
            ShippingCountry = request.ShippingCountry,
            ShippingRegion = request.ShippingRegion,
            ShippingMethod = request.ShippingMethod,
            Metadata = request.Metadata,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Create order items
        decimal subtotal = 0;
        foreach (var item in request.Items)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
            {
                return OrderResult.Fail($"Product {item.ProductId} not found");
            }

            decimal unitPrice;
            string? color = null, size = null, sku = null;

            if (item.VariationId.HasValue)
            {
                var variation = product.Variations.FirstOrDefault(v => v.Id == item.VariationId);
                if (variation == null)
                {
                    return OrderResult.Fail($"Variation {item.VariationId} not found");
                }
                unitPrice = variation.Price;
                color = variation.Color;
                size = variation.Size;
                sku = variation.Sku;
            }
            else
            {
                unitPrice = product.Price;
                color = product.Color;
                size = product.Size;
            }

            var lineTotal = unitPrice * item.Quantity;
            subtotal += lineTotal;

            order.Items.Add(new OrderItemEntity
            {
                ProductId = item.ProductId,
                VariationId = item.VariationId,
                ProductName = product.Name,
                ProductImageUrl = product.ImageUrl,
                Sku = sku,
                Color = color,
                Size = size,
                Quantity = item.Quantity,
                UnitPrice = unitPrice,
                OriginalPrice = product.OriginalPrice,
                LineTotal = lineTotal,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Calculate totals
        order.Subtotal = subtotal;
        order.ItemCount = request.Items.Sum(i => i.Quantity);
        order.Total = subtotal + order.ShippingCost + order.TaxAmount - order.DiscountAmount;

        // Save order
        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        // Deduct inventory
        var deductResult = await _inventory.DeductStockAsync(stockItems, order.Id, ct);
        if (!deductResult.Success)
        {
            _logger.LogWarning("Failed to deduct inventory for order {OrderId}: {Error}",
                order.Id, deductResult.Error);
            // Order still created, but inventory issue logged
        }

        // Publish event
        _events.Publish(new OrderPlacedEvent(
            order.Id,
            order.OrderNumber,
            order.ProfileId,
            order.Total,
            order.ItemCount));

        _logger.LogInformation("Created order {OrderNumber} with {ItemCount} items, total {Total}",
            orderNumber, order.ItemCount, order.Total);

        return OrderResult.Ok(order);
    }

    public async Task<OrderEntity?> GetByIdAsync(int orderId, CancellationToken ct = default)
    {
        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);
    }

    public async Task<OrderEntity?> GetByOrderNumberAsync(string orderNumber, CancellationToken ct = default)
    {
        return await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.OrderNumber == orderNumber, ct);
    }

    public async Task<List<OrderEntity>> GetByProfileAsync(Guid profileId, int limit = 10, CancellationToken ct = default)
    {
        return await _db.Orders
            .Where(o => o.ProfileId == profileId)
            .OrderByDescending(o => o.CreatedAt)
            .Take(limit)
            .Include(o => o.Items)
            .ToListAsync(ct);
    }

    public async Task<OrderResult> TransitionStatusAsync(
        int orderId, 
        OrderStatus newStatus, 
        string? reason = null,
        CancellationToken ct = default)
    {
        var order = await _db.Orders.FindAsync([orderId], ct);
        if (order == null)
        {
            return OrderResult.Fail($"Order {orderId} not found");
        }

        if (!OrderStateTransitions.CanTransition(order.Status, newStatus))
        {
            var allowed = OrderStateTransitions.GetAllowedTransitions(order.Status);
            return OrderResult.Fail(
                $"Cannot transition from {order.Status} to {newStatus}. " +
                $"Allowed: {string.Join(", ", allowed)}");
        }

        var oldStatus = order.Status;
        order.Status = newStatus;
        order.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Order {OrderNumber} transitioned from {OldStatus} to {NewStatus}",
            order.OrderNumber, oldStatus, newStatus);

        return OrderResult.Ok(order);
    }

    public async Task<OrderResult> ConfirmOrderAsync(int orderId, CancellationToken ct = default)
    {
        var result = await TransitionStatusAsync(orderId, OrderStatus.Confirmed, null, ct);
        if (result.Success && result.Order != null)
        {
            _events.Publish(new OrderConfirmedEvent(result.Order.Id, result.Order.OrderNumber));
        }
        return result;
    }

    public async Task<OrderResult> ShipOrderAsync(int orderId, string? trackingNumber = null, CancellationToken ct = default)
    {
        var order = await _db.Orders.FindAsync([orderId], ct);
        if (order == null)
        {
            return OrderResult.Fail($"Order {orderId} not found");
        }

        // First transition to Processing if needed
        if (order.Status == OrderStatus.Confirmed)
        {
            order.Status = OrderStatus.Processing;
        }

        var result = await TransitionStatusAsync(orderId, OrderStatus.Shipped, null, ct);
        if (result.Success && result.Order != null)
        {
            result.Order.FulfillmentStatus = FulfillmentStatus.Fulfilled;
            await _db.SaveChangesAsync(ct);

            _events.Publish(new OrderShippedEvent(
                result.Order.Id, 
                result.Order.OrderNumber, 
                trackingNumber));
        }
        return result;
    }

    public async Task<OrderResult> DeliverOrderAsync(int orderId, CancellationToken ct = default)
    {
        var result = await TransitionStatusAsync(orderId, OrderStatus.Delivered, null, ct);
        if (result.Success && result.Order != null)
        {
            _events.Publish(new OrderDeliveredEvent(result.Order.Id, result.Order.OrderNumber));
        }
        return result;
    }

    public async Task<OrderResult> CancelOrderAsync(int orderId, string reason, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null)
        {
            return OrderResult.Fail($"Order {orderId} not found");
        }

        var result = await TransitionStatusAsync(orderId, OrderStatus.Cancelled, reason, ct);
        if (!result.Success)
        {
            return result;
        }

        // Return inventory
        var items = order.Items
            .Select(i => (i.ProductId, i.VariationId, i.Quantity))
            .ToList();

        await _inventory.ReturnStockAsync(items, orderId, ct);

        _events.Publish(new OrderCancelledEvent(order.Id, order.OrderNumber, reason));

        return result;
    }

    public async Task<OrderResult> RefundOrderAsync(int orderId, decimal? partialAmount = null, CancellationToken ct = default)
    {
        var order = await _db.Orders
            .Include(o => o.Items)
            .FirstOrDefaultAsync(o => o.Id == orderId, ct);

        if (order == null)
        {
            return OrderResult.Fail($"Order {orderId} not found");
        }

        var refundAmount = partialAmount ?? order.Total;

        var result = await TransitionStatusAsync(orderId, OrderStatus.Refunded, null, ct);
        if (!result.Success)
        {
            return result;
        }

        order.PaymentStatus = partialAmount.HasValue && partialAmount < order.Total
            ? PaymentStatus.PartiallyRefunded
            : PaymentStatus.Refunded;

        await _db.SaveChangesAsync(ct);

        // Return inventory for full refunds
        if (!partialAmount.HasValue || partialAmount >= order.Total)
        {
            var items = order.Items
                .Select(i => (i.ProductId, i.VariationId, i.Quantity))
                .ToList();

            await _inventory.ReturnStockAsync(items, orderId, ct);
        }

        _events.Publish(new OrderRefundedEvent(order.Id, order.OrderNumber, refundAmount));

        return result;
    }

    private static string GenerateOrderNumber()
    {
        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd");
        var random = Random.Shared.Next(100000, 999999);
        return $"ORD-{timestamp}-{random}";
    }
}
