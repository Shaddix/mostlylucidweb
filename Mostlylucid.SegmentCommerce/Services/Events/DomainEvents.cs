namespace Mostlylucid.SegmentCommerce.Services.Events;

/// <summary>
/// Base interface for all domain events.
/// </summary>
public interface IDomainEvent
{
    Guid EventId { get; }
    DateTime OccurredAt { get; }
}

/// <summary>
/// Base record for domain events with common properties.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
}

// ============ ORDER EVENTS ============

public record OrderPlacedEvent(
    int OrderId,
    string OrderNumber,
    Guid? ProfileId,
    decimal Total,
    int ItemCount) : DomainEvent;

public record OrderConfirmedEvent(
    int OrderId,
    string OrderNumber) : DomainEvent;

public record OrderShippedEvent(
    int OrderId,
    string OrderNumber,
    string? TrackingNumber) : DomainEvent;

public record OrderDeliveredEvent(
    int OrderId,
    string OrderNumber) : DomainEvent;

public record OrderCancelledEvent(
    int OrderId,
    string OrderNumber,
    string Reason) : DomainEvent;

public record OrderRefundedEvent(
    int OrderId,
    string OrderNumber,
    decimal RefundAmount) : DomainEvent;

// ============ CART EVENTS ============

public record CartItemAddedEvent(
    string SessionId,
    Guid? ProfileId,
    int ProductId,
    int? VariationId,
    int Quantity,
    decimal UnitPrice) : DomainEvent;

public record CartItemRemovedEvent(
    string SessionId,
    Guid? ProfileId,
    int ProductId,
    int? VariationId) : DomainEvent;

public record CartAbandonedEvent(
    string SessionId,
    Guid? ProfileId,
    decimal CartTotal,
    int ItemCount) : DomainEvent;

// ============ INVENTORY EVENTS ============

public record StockReservedEvent(
    int ProductId,
    int? VariationId,
    int Quantity,
    string ReservationId) : DomainEvent;

public record StockReservationReleasedEvent(
    string ReservationId) : DomainEvent;

public record StockDeductedEvent(
    int ProductId,
    int? VariationId,
    int Quantity,
    int OrderId) : DomainEvent;

public record LowStockAlertEvent(
    int ProductId,
    int? VariationId,
    int CurrentStock,
    int Threshold) : DomainEvent;

// ============ PAYMENT EVENTS ============

public record PaymentAuthorizedEvent(
    int OrderId,
    string PaymentId,
    decimal Amount,
    string Currency) : DomainEvent;

public record PaymentCapturedEvent(
    int OrderId,
    string PaymentId,
    decimal Amount) : DomainEvent;

public record PaymentFailedEvent(
    int OrderId,
    string? PaymentId,
    string ErrorMessage) : DomainEvent;

public record PaymentRefundedEvent(
    int OrderId,
    string PaymentId,
    decimal RefundAmount) : DomainEvent;

// ============ PROFILE/SIGNAL EVENTS ============

public record ProfileSignalRecordedEvent(
    Guid? ProfileId,
    string SessionId,
    string SignalType,
    string? Category,
    int? ProductId,
    double Weight) : DomainEvent;

public record ProfileSegmentChangedEvent(
    Guid ProfileId,
    List<string> OldSegments,
    List<string> NewSegments) : DomainEvent;

public record ProfileInterestDecayedEvent(
    Guid ProfileId,
    Dictionary<string, double> DecayedInterests) : DomainEvent;
