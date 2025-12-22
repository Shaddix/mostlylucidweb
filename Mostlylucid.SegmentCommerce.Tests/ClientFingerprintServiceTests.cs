using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Mostlylucid.SegmentCommerce.ClientFingerprint;
using Xunit;

namespace Mostlylucid.SegmentCommerce.Tests;

public class ClientFingerprintServiceTests
{
    private static IOptions<ClientFingerprintConfig> CreateOptions(string? hmacKey = null)
    {
        var config = new ClientFingerprintConfig
        {
            Enabled = true,
            HmacKey = hmacKey ?? Convert.ToBase64String(new byte[32]) // 256-bit key
        };
        return Options.Create(config);
    }

    [Fact]
    public void GenerateSessionId_IsStable_ForSameInput()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        
        var id1 = service.GenerateSessionId("client-hash-123");
        var id2 = service.GenerateSessionId("client-hash-123");
        
        Assert.Equal(id1, id2);
    }

    [Fact]
    public void GenerateSessionId_IsDifferent_ForDifferentInputs()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        
        var id1 = service.GenerateSessionId("hash-a");
        var id2 = service.GenerateSessionId("hash-b");
        
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void GenerateSessionId_ReturnsEmpty_ForEmptyInput()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        
        var id = service.GenerateSessionId("");
        
        Assert.Equal(string.Empty, id);
    }

    [Fact]
    public void GenerateSessionId_ReturnsEmpty_ForNullInput()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        
        var id = service.GenerateSessionId(null!);
        
        Assert.Equal(string.Empty, id);
    }

    [Fact]
    public void GenerateSessionId_IsUrlSafe()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        
        // Test many inputs to ensure URL safety
        for (int i = 0; i < 100; i++)
        {
            var id = service.GenerateSessionId($"test-hash-{i}");
            Assert.DoesNotContain("+", id);
            Assert.DoesNotContain("/", id);
            Assert.DoesNotContain("=", id);
        }
    }

    [Fact]
    public void GenerateSessionId_Has22Characters()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        
        var id = service.GenerateSessionId("any-hash");
        
        Assert.Equal(22, id.Length);
    }

    [Fact]
    public void GenerateSessionId_IsDifferent_WithDifferentKeys()
    {
        var key1 = Convert.ToBase64String(new byte[32]);
        var key2 = Convert.ToBase64String(Enumerable.Repeat((byte)1, 32).ToArray());
        
        var service1 = new ClientFingerprintService(CreateOptions(key1), NullLogger<ClientFingerprintService>.Instance);
        var service2 = new ClientFingerprintService(CreateOptions(key2), NullLogger<ClientFingerprintService>.Instance);
        
        var id1 = service1.GenerateSessionId("same-hash");
        var id2 = service2.GenerateSessionId("same-hash");
        
        Assert.NotEqual(id1, id2);
    }

    [Fact]
    public void SetAndGetSessionId_RoundTrips()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        var context = new DefaultHttpContext();
        
        service.SetSessionId(context, "session-123");
        var retrieved = service.GetSessionId(context);
        
        Assert.Equal("session-123", retrieved);
    }

    [Fact]
    public void GetSessionId_ReturnsNull_WhenNotSet()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        var context = new DefaultHttpContext();
        
        var id = service.GetSessionId(context);
        
        Assert.Null(id);
    }

    [Fact]
    public void SetSessionId_DoesNothing_ForEmptyId()
    {
        var service = new ClientFingerprintService(CreateOptions(), NullLogger<ClientFingerprintService>.Instance);
        var context = new DefaultHttpContext();
        
        service.SetSessionId(context, "");
        var id = service.GetSessionId(context);
        
        Assert.Null(id);
    }

    [Fact]
    public void Constructor_GeneratesEphemeralKey_WhenNotConfigured()
    {
        var config = new ClientFingerprintConfig { Enabled = true, HmacKey = null };
        var options = Options.Create(config);
        
        // Should not throw - generates ephemeral key
        var service = new ClientFingerprintService(options, NullLogger<ClientFingerprintService>.Instance);
        var id = service.GenerateSessionId("test");
        
        Assert.NotEmpty(id);
    }
}
