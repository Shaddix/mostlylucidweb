using Mostlylucid.SegmentCommerce.Data.Entities;
using Mostlylucid.SegmentCommerce.Services.Queue;

namespace Mostlylucid.SegmentCommerce.Services.Events;

/// <summary>
/// Service for publishing domain events to the outbox.
/// Events are stored transactionally and processed asynchronously.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Queue an event to be published when SaveChanges is called.
    /// </summary>
    void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent;
    
    /// <summary>
    /// Publish an event and save immediately.
    /// </summary>
    Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IDomainEvent;
}

/// <summary>
/// Implementation that publishes events to the PostgreSQL outbox.
/// </summary>
public class OutboxEventPublisher : IEventPublisher
{
    private readonly IOutbox _outbox;
    private readonly ILogger<OutboxEventPublisher> _logger;

    public OutboxEventPublisher(IOutbox outbox, ILogger<OutboxEventPublisher> logger)
    {
        _outbox = outbox;
        _logger = logger;
    }

    public void Publish<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        var eventType = GetEventType(@event);
        var (aggregateType, aggregateId) = GetAggregateInfo(@event);
        
        _outbox.Publish(eventType, @event, aggregateType, aggregateId);
        
        _logger.LogDebug("Queued event {EventType} with ID {EventId}", eventType, @event.EventId);
    }

    public async Task PublishAsync<TEvent>(TEvent @event, CancellationToken ct = default) where TEvent : IDomainEvent
    {
        var eventType = GetEventType(@event);
        var (aggregateType, aggregateId) = GetAggregateInfo(@event);
        
        await _outbox.PublishAndSaveAsync(eventType, @event, aggregateType, aggregateId, ct);
        
        _logger.LogDebug("Published event {EventType} with ID {EventId}", eventType, @event.EventId);
    }

    private static string GetEventType<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        // Convert PascalCase to snake_case event type
        // e.g., OrderPlacedEvent -> order.placed
        var typeName = typeof(TEvent).Name;
        if (typeName.EndsWith("Event"))
        {
            typeName = typeName[..^5]; // Remove "Event" suffix
        }

        return typeName switch
        {
            // Order events
            "OrderPlaced" => OutboxEventTypes.OrderPlaced,
            "OrderConfirmed" => "order.confirmed",
            "OrderShipped" => "order.shipped",
            "OrderDelivered" => "order.delivered",
            "OrderCancelled" => "order.cancelled",
            "OrderRefunded" => "order.refunded",
            
            // Cart events
            "CartItemAdded" => OutboxEventTypes.ProductAddedToCart,
            "CartItemRemoved" => OutboxEventTypes.ProductRemovedFromCart,
            "CartAbandoned" => "cart.abandoned",
            
            // Inventory events
            "StockReserved" => "inventory.reserved",
            "StockReservationReleased" => "inventory.reservation_released",
            "StockDeducted" => "inventory.deducted",
            "LowStockAlert" => "inventory.low_stock",
            
            // Payment events
            "PaymentAuthorized" => "payment.authorized",
            "PaymentCaptured" => "payment.captured",
            "PaymentFailed" => "payment.failed",
            "PaymentRefunded" => "payment.refunded",
            
            // Profile events
            "ProfileSignalRecorded" => "signal.recorded",
            "ProfileSegmentChanged" => "profile.segment_changed",
            "ProfileInterestDecayed" => "profile.interest_decayed",
            
            _ => $"domain.{ToSnakeCase(typeName)}"
        };
    }

    private static (string? aggregateType, string? aggregateId) GetAggregateInfo<TEvent>(TEvent @event) where TEvent : IDomainEvent
    {
        return @event switch
        {
            OrderPlacedEvent e => ("Order", e.OrderId.ToString()),
            OrderConfirmedEvent e => ("Order", e.OrderId.ToString()),
            OrderShippedEvent e => ("Order", e.OrderId.ToString()),
            OrderDeliveredEvent e => ("Order", e.OrderId.ToString()),
            OrderCancelledEvent e => ("Order", e.OrderId.ToString()),
            OrderRefundedEvent e => ("Order", e.OrderId.ToString()),
            
            CartItemAddedEvent e => ("Cart", e.SessionId),
            CartItemRemovedEvent e => ("Cart", e.SessionId),
            CartAbandonedEvent e => ("Cart", e.SessionId),
            
            StockReservedEvent e => ("Product", e.ProductId.ToString()),
            StockDeductedEvent e => ("Product", e.ProductId.ToString()),
            LowStockAlertEvent e => ("Product", e.ProductId.ToString()),
            
            PaymentAuthorizedEvent e => ("Order", e.OrderId.ToString()),
            PaymentCapturedEvent e => ("Order", e.OrderId.ToString()),
            PaymentFailedEvent e => ("Order", e.OrderId.ToString()),
            
            ProfileSignalRecordedEvent e => ("Profile", e.ProfileId?.ToString() ?? e.SessionId),
            ProfileSegmentChangedEvent e => ("Profile", e.ProfileId.ToString()),
            
            _ => (null, null)
        };
    }

    private static string ToSnakeCase(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;
        
        var result = new System.Text.StringBuilder();
        for (int i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (char.IsUpper(c) && i > 0)
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(c));
        }
        return result.ToString();
    }
}
