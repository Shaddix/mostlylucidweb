using Mostlylucid.SegmentCommerce.Models;

namespace Mostlylucid.SegmentCommerce.Services;

public interface IInterestTrackingService
{
    Task TrackCategoryInterestAsync(string category, double weight = 0.1);
    Task TrackPurchaseIntentAsync(string category, double weight = 0.4);
    InterestSignature GetCurrentSignature();
}

public class InterestTrackingService(ISessionService sessionService) : IInterestTrackingService
{
    public async Task TrackCategoryInterestAsync(string category, double weight = 0.1)
    {
        var signature = sessionService.GetInterestSignature();

        if (signature.Interests.TryGetValue(category, out var existing))
        {
            existing.Weight = Math.Min(1.0, existing.Weight + weight);
            existing.LastReinforced = DateTime.UtcNow;
            existing.ReinforcementCount++;
        }
        else
        {
            signature.Interests[category] = new InterestWeight
            {
                Category = category,
                Weight = weight,
                LastReinforced = DateTime.UtcNow,
                ReinforcementCount = 1
            };
        }

        sessionService.SaveInterestSignature(signature);
        await Task.CompletedTask;
    }

    public async Task TrackPurchaseIntentAsync(string category, double weight = 0.4)
    {
        var signature = sessionService.GetInterestSignature();

        if (signature.Interests.TryGetValue(category, out var existing))
        {
            existing.Weight = Math.Min(1.0, existing.Weight + weight);
            existing.LastReinforced = DateTime.UtcNow;
            existing.ReinforcementCount++;
        }
        else
        {
            signature.Interests[category] = new InterestWeight
            {
                Category = category,
                Weight = weight,
                LastReinforced = DateTime.UtcNow,
                ReinforcementCount = 1
            };
        }

        sessionService.SaveInterestSignature(signature);
        await Task.CompletedTask;
    }

    public InterestSignature GetCurrentSignature()
    {
        return sessionService.GetInterestSignature();
    }
}
