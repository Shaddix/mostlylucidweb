using Microsoft.EntityFrameworkCore;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Events;

namespace Mostlylucid.SegmentCommerce.Services.Inventory;

/// <summary>
/// Result of an inventory operation.
/// </summary>
public record InventoryResult(bool Success, string? Error = null)
{
    public static InventoryResult Ok() => new(true);
    public static InventoryResult Fail(string error) => new(false, error);
}

/// <summary>
/// Stock availability check result.
/// </summary>
public record StockAvailability(
    int ProductId,
    int? VariationId,
    int RequestedQuantity,
    int AvailableQuantity,
    bool IsAvailable)
{
    public int Shortage => IsAvailable ? 0 : RequestedQuantity - AvailableQuantity;
}

/// <summary>
/// A stock reservation that holds inventory for a checkout.
/// </summary>
public record StockReservation(
    string ReservationId,
    int ProductId,
    int? VariationId,
    int Quantity,
    DateTime ExpiresAt);

/// <summary>
/// Service for managing product inventory with stock validation and reservations.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Check if requested quantities are available.
    /// </summary>
    Task<List<StockAvailability>> CheckAvailabilityAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        CancellationToken ct = default);

    /// <summary>
    /// Reserve stock during checkout (with expiration).
    /// </summary>
    Task<(InventoryResult Result, List<StockReservation>? Reservations)> ReserveStockAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        TimeSpan? reservationDuration = null,
        CancellationToken ct = default);

    /// <summary>
    /// Release a reservation (e.g., on checkout abandonment).
    /// </summary>
    Task<InventoryResult> ReleaseReservationAsync(string reservationId, CancellationToken ct = default);

    /// <summary>
    /// Commit a reservation to permanent stock deduction (on order completion).
    /// </summary>
    Task<InventoryResult> CommitReservationAsync(string reservationId, int orderId, CancellationToken ct = default);

    /// <summary>
    /// Directly deduct stock (for immediate purchases without reservation).
    /// </summary>
    Task<InventoryResult> DeductStockAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        int orderId,
        CancellationToken ct = default);

    /// <summary>
    /// Return stock (for refunds/cancellations).
    /// </summary>
    Task<InventoryResult> ReturnStockAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        int orderId,
        CancellationToken ct = default);
}

public class InventoryService : IInventoryService
{
    private readonly SegmentCommerceDbContext _db;
    private readonly IEventPublisher _events;
    private readonly ILogger<InventoryService> _logger;
    private readonly int _lowStockThreshold;

    // In-memory reservation store (in production, use Redis or database table)
    private static readonly Dictionary<string, StockReservation> _reservations = new();
    private static readonly object _lock = new();

    public InventoryService(
        SegmentCommerceDbContext db,
        IEventPublisher events,
        ILogger<InventoryService> logger,
        IConfiguration config)
    {
        _db = db;
        _events = events;
        _logger = logger;
        _lowStockThreshold = config.GetValue("Inventory:LowStockThreshold", 10);
    }

    public async Task<List<StockAvailability>> CheckAvailabilityAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        CancellationToken ct = default)
    {
        var results = new List<StockAvailability>();
        var itemList = items.ToList();

        // Batch load all products and variations we need
        var productIds = itemList.Select(i => i.ProductId).Distinct().ToList();
        var variationIds = itemList.Where(i => i.VariationId.HasValue)
            .Select(i => i.VariationId!.Value).Distinct().ToList();

        var products = await _db.Products
            .Where(p => productIds.Contains(p.Id))
            .Include(p => p.Variations)
            .ToDictionaryAsync(p => p.Id, ct);

        foreach (var (productId, variationId, quantity) in itemList)
        {
            if (!products.TryGetValue(productId, out var product))
            {
                results.Add(new StockAvailability(productId, variationId, quantity, 0, false));
                continue;
            }

            int available;
            if (variationId.HasValue)
            {
                var variation = product.Variations.FirstOrDefault(v => v.Id == variationId.Value);
                available = variation?.StockQuantity ?? 0;
            }
            else
            {
                // Sum all variation stock, or use a default stock level
                available = product.Variations.Any() 
                    ? product.Variations.Sum(v => v.StockQuantity)
                    : int.MaxValue; // No variations = unlimited stock (configurable)
            }

            // Account for reservations
            var reservedQty = GetReservedQuantity(productId, variationId);
            var effectiveAvailable = Math.Max(0, available - reservedQty);

            results.Add(new StockAvailability(
                productId, 
                variationId, 
                quantity, 
                effectiveAvailable,
                effectiveAvailable >= quantity));
        }

        return results;
    }

    public async Task<(InventoryResult Result, List<StockReservation>? Reservations)> ReserveStockAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        TimeSpan? reservationDuration = null,
        CancellationToken ct = default)
    {
        var itemList = items.ToList();
        var duration = reservationDuration ?? TimeSpan.FromMinutes(15);

        // Check availability first
        var availability = await CheckAvailabilityAsync(itemList, ct);
        var unavailable = availability.Where(a => !a.IsAvailable).ToList();

        if (unavailable.Any())
        {
            var errors = string.Join(", ", unavailable.Select(u => 
                $"Product {u.ProductId}: need {u.RequestedQuantity}, have {u.AvailableQuantity}"));
            return (InventoryResult.Fail($"Insufficient stock: {errors}"), null);
        }

        // Create reservations
        var reservations = new List<StockReservation>();
        var expiresAt = DateTime.UtcNow.Add(duration);

        lock (_lock)
        {
            foreach (var (productId, variationId, quantity) in itemList)
            {
                var reservationId = $"res_{Guid.NewGuid():N}";
                var reservation = new StockReservation(
                    reservationId, 
                    productId, 
                    variationId, 
                    quantity, 
                    expiresAt);
                
                _reservations[reservationId] = reservation;
                reservations.Add(reservation);

                _events.Publish(new StockReservedEvent(
                    productId, variationId, quantity, reservationId));
            }
        }

        _logger.LogInformation("Reserved stock for {Count} items, expires at {ExpiresAt}",
            reservations.Count, expiresAt);

        return (InventoryResult.Ok(), reservations);
    }

    public Task<InventoryResult> ReleaseReservationAsync(string reservationId, CancellationToken ct = default)
    {
        lock (_lock)
        {
            if (_reservations.Remove(reservationId, out var reservation))
            {
                _events.Publish(new StockReservationReleasedEvent(reservationId));
                _logger.LogInformation("Released reservation {ReservationId}", reservationId);
                return Task.FromResult(InventoryResult.Ok());
            }
        }

        return Task.FromResult(InventoryResult.Fail($"Reservation {reservationId} not found"));
    }

    public async Task<InventoryResult> CommitReservationAsync(string reservationId, int orderId, CancellationToken ct = default)
    {
        StockReservation? reservation;
        lock (_lock)
        {
            if (!_reservations.TryGetValue(reservationId, out reservation))
            {
                return InventoryResult.Fail($"Reservation {reservationId} not found or expired");
            }

            if (reservation.ExpiresAt < DateTime.UtcNow)
            {
                _reservations.Remove(reservationId);
                return InventoryResult.Fail("Reservation has expired");
            }

            _reservations.Remove(reservationId);
        }

        // Actually deduct the stock
        return await DeductStockInternalAsync(
            reservation.ProductId, 
            reservation.VariationId, 
            reservation.Quantity,
            orderId,
            ct);
    }

    public async Task<InventoryResult> DeductStockAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        int orderId,
        CancellationToken ct = default)
    {
        foreach (var (productId, variationId, quantity) in items)
        {
            var result = await DeductStockInternalAsync(productId, variationId, quantity, orderId, ct);
            if (!result.Success)
            {
                return result;
            }
        }

        return InventoryResult.Ok();
    }

    public async Task<InventoryResult> ReturnStockAsync(
        IEnumerable<(int ProductId, int? VariationId, int Quantity)> items,
        int orderId,
        CancellationToken ct = default)
    {
        foreach (var (productId, variationId, quantity) in items)
        {
            if (variationId.HasValue)
            {
                var variation = await _db.Set<ProductVariationEntity>()
                    .FirstOrDefaultAsync(v => v.Id == variationId.Value, ct);
                
                if (variation != null)
                {
                    variation.StockQuantity += quantity;
                    variation.UpdatedAt = DateTime.UtcNow;
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Returned stock for order {OrderId}", orderId);
        return InventoryResult.Ok();
    }

    private async Task<InventoryResult> DeductStockInternalAsync(
        int productId,
        int? variationId,
        int quantity,
        int orderId,
        CancellationToken ct)
    {
        if (variationId.HasValue)
        {
            var variation = await _db.Set<ProductVariationEntity>()
                .FirstOrDefaultAsync(v => v.Id == variationId.Value, ct);

            if (variation == null)
            {
                return InventoryResult.Fail($"Variation {variationId} not found");
            }

            if (variation.StockQuantity < quantity)
            {
                return InventoryResult.Fail(
                    $"Insufficient stock for variation {variationId}: need {quantity}, have {variation.StockQuantity}");
            }

            variation.StockQuantity -= quantity;
            variation.UpdatedAt = DateTime.UtcNow;

            // Update availability status
            if (variation.StockQuantity == 0)
            {
                variation.AvailabilityStatus = AvailabilityStatus.OutOfStock;
            }
            else if (variation.StockQuantity <= _lowStockThreshold)
            {
                _events.Publish(new LowStockAlertEvent(
                    productId, variationId, variation.StockQuantity, _lowStockThreshold));
            }
        }

        await _db.SaveChangesAsync(ct);

        _events.Publish(new StockDeductedEvent(productId, variationId, quantity, orderId));
        _logger.LogInformation("Deducted {Quantity} from product {ProductId} for order {OrderId}",
            quantity, productId, orderId);

        return InventoryResult.Ok();
    }

    private int GetReservedQuantity(int productId, int? variationId)
    {
        lock (_lock)
        {
            // Clean up expired reservations while we're at it
            var expired = _reservations
                .Where(r => r.Value.ExpiresAt < DateTime.UtcNow)
                .Select(r => r.Key)
                .ToList();
            
            foreach (var key in expired)
            {
                _reservations.Remove(key);
            }

            return _reservations.Values
                .Where(r => r.ProductId == productId && r.VariationId == variationId)
                .Sum(r => r.Quantity);
        }
    }
}
