using System.Security.Cryptography;
using System.Text;
using Mostlylucid.SegmentCommerce.SampleData.Models;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;

namespace Mostlylucid.SegmentCommerce.SampleData.Services;

public class ProfileGenerator
{
    private readonly GadgetTaxonomy _taxonomy;
    private readonly Random _random = new();

    public ProfileGenerator(GadgetTaxonomy taxonomy)
    {
        _taxonomy = taxonomy;
    }

    public List<GeneratedProfile> GenerateProfiles(int count, IEnumerable<string> categories)
    {
        var categoryList = categories.ToList();
        var profiles = new List<GeneratedProfile>();

        for (var i = 0; i < count; i++)
        {
            var profileKey = Hash($"fp-{Guid.NewGuid():N}");
            var profileInterests = new Dictionary<string, double>();
            var signals = new List<GeneratedSignal>();
            var segments = new List<GeneratedProfileSegment>();

            // pick 2-4 categories
            var chosen = categoryList.OrderBy(_ => _random.Next()).Take(_random.Next(2, Math.Min(5, categoryList.Count))).ToList();
            foreach (var cat in chosen)
            {
                var weight = Math.Round(_random.NextDouble() * 0.8 + 0.1, 2);
                profileInterests[cat] = weight;

                signals.Add(new GeneratedSignal
                {
                    SignalType = SignalTypes.ProductView,
                    Category = cat,
                    Weight = Math.Round(weight * 0.8, 2)
                });

                signals.Add(new GeneratedSignal
                {
                    SignalType = SignalTypes.AddToCart,
                    Category = cat,
                    Weight = Math.Round(weight * 0.5, 2)
                });

                // occasional purchase
                if (_random.NextDouble() > 0.65)
                {
                    signals.Add(new GeneratedSignal
                    {
                        SignalType = SignalTypes.Purchase,
                        Category = cat,
                        Weight = Math.Round(weight, 2)
                    });
                }
            }

            // segment labels for narrative
            segments.Add(new GeneratedProfileSegment
            {
                Name = "value_shopper",
                Score = Math.Round(_random.NextDouble(), 2)
            });
            segments.Add(new GeneratedProfileSegment
            {
                Name = "brand_loyal",
                Score = Math.Round(_random.NextDouble(), 2)
            });

            profiles.Add(new GeneratedProfile
            {
                ProfileKey = profileKey,
                Interests = profileInterests,
                Signals = signals,
                Segments = segments
            });
        }

        return profiles;
    }

    private static string Hash(string input)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
