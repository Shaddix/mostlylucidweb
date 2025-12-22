using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Mostlylucid.SegmentCommerce.Data;
using Mostlylucid.SegmentCommerce.Data.Entities.Profiles;
using Mostlylucid.SegmentCommerce.Services.Profiles;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class ProfileKeyServiceTests
{
    private static SegmentCommerceDbContext CreateContext() => TestDbContextBase.Create();

    private static IConfiguration CreateConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Profiles:KeySecret"] = "unit-test-secret-key-for-testing"
        })
        .Build();

    [Fact]
    public void GenerateKey_IsStable_ForSameInput()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest("fp-hash", "cookie-id", "user-id");
        var key1 = service.GenerateKey(request);
        var key2 = service.GenerateKey(request);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateKey_PrioritizesUserId_OverOtherIdentifiers()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var withAll = new ProfileKeyRequest("fp", "cookie", "user123");
        var userOnly = new ProfileKeyRequest(null, null, "user123");
        
        var key1 = service.GenerateKey(withAll);
        var key2 = service.GenerateKey(userOnly);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateKey_PrioritizesCookieId_OverFingerprint()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var withBoth = new ProfileKeyRequest("fp", "cookie123", null);
        var cookieOnly = new ProfileKeyRequest(null, "cookie123", null);
        
        var key1 = service.GenerateKey(withBoth);
        var key2 = service.GenerateKey(cookieOnly);

        Assert.Equal(key1, key2);
    }

    [Fact]
    public void GenerateKey_IsDifferent_ForDifferentInputs()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request1 = new ProfileKeyRequest("fp1", null, null);
        var request2 = new ProfileKeyRequest("fp2", null, null);
        
        var key1 = service.GenerateKey(request1);
        var key2 = service.GenerateKey(request2);

        Assert.NotEqual(key1, key2);
    }

    [Fact]
    public void GenerateKey_ThrowsException_WhenNoIdentifiers()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest(null, null, null);

        Assert.Throws<ArgumentException>(() => service.GenerateKey(request));
    }

    [Fact]
    public async Task GetOrCreateAsync_CreatesNewProfile_WhenNotExists()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest("fp-hash-new", null, null);
        var result = await service.GetOrCreateAsync(request);

        Assert.True(result.WasCreated);
        Assert.NotNull(result.Profile);
        Assert.Equal(result.KeyHash, result.Profile.ProfileKey);
        Assert.Equal(ProfileIdentificationMode.Fingerprint, result.Profile.IdentificationMode);
    }

    [Fact]
    public async Task GetOrCreateAsync_ReturnsExistingProfile_WhenExists()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest("fp-hash-existing", null, null);
        
        // Create first
        var first = await service.GetOrCreateAsync(request);
        Assert.True(first.WasCreated);
        
        // Get second time
        var second = await service.GetOrCreateAsync(request);
        Assert.False(second.WasCreated);
        Assert.Equal(first.Profile.Id, second.Profile.Id);
    }

    [Fact]
    public async Task GetOrCreateAsync_SetsCorrectMode_ForIdentityUser()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest(null, null, "user-123");
        var result = await service.GetOrCreateAsync(request);

        Assert.Equal(ProfileIdentificationMode.Identity, result.Profile.IdentificationMode);
    }

    [Fact]
    public async Task GetOrCreateAsync_SetsCorrectMode_ForCookieUser()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        var request = new ProfileKeyRequest(null, "cookie-abc", null);
        var result = await service.GetOrCreateAsync(request);

        Assert.Equal(ProfileIdentificationMode.Cookie, result.Profile.IdentificationMode);
    }

    [Fact]
    public async Task LinkAlternateKeyAsync_AddsNewKey_ToExistingProfile()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        // Create profile with fingerprint
        var fpRequest = new ProfileKeyRequest("fp-main", null, null);
        var result = await service.GetOrCreateAsync(fpRequest);
        
        // Link cookie key
        var cookieRequest = new ProfileKeyRequest(null, "cookie-linked", null);
        await service.LinkAlternateKeyAsync(result.Profile.Id, cookieRequest);

        // Verify key was added
        var keys = await context.ProfileKeys.Where(k => k.ProfileId == result.Profile.Id).ToListAsync();
        Assert.Single(keys);
        Assert.Equal(ProfileIdentificationMode.Cookie, keys[0].KeyType);
    }

    [Fact]
    public async Task GetOrCreateAsync_FindsProfile_ViaAlternateKey()
    {
        using var context = CreateContext();
        var service = new ProfileKeyService(context, NullLogger<ProfileKeyService>.Instance, CreateConfig());

        // Create profile with fingerprint
        var fpRequest = new ProfileKeyRequest("fp-original", null, null);
        var original = await service.GetOrCreateAsync(fpRequest);
        
        // Link cookie key
        var cookieRequest = new ProfileKeyRequest(null, "cookie-alt", null);
        await service.LinkAlternateKeyAsync(original.Profile.Id, cookieRequest);

        // Find via cookie key
        var found = await service.GetOrCreateAsync(cookieRequest);
        
        Assert.False(found.WasCreated);
        Assert.Equal(original.Profile.Id, found.Profile.Id);
    }

}
