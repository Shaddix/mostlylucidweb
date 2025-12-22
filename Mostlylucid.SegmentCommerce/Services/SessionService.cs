using Mostlylucid.SegmentCommerce.Models;

namespace Mostlylucid.SegmentCommerce.Services;

public interface ISessionService
{
    string GetSessionId();
    InterestSignature GetInterestSignature();
    void SaveInterestSignature(InterestSignature signature);
    void ClearInterestSignature();
}

public class SessionService(IHttpContextAccessor httpContextAccessor) : ISessionService
{
    private const string SessionIdKey = "SessionId";
    private const string InterestSignatureKey = "InterestSignature";
    
    private ISession Session => httpContextAccessor.HttpContext?.Session 
        ?? throw new InvalidOperationException("HttpContext is not available");

    public string GetSessionId()
    {
        var sessionId = Session.GetString(SessionIdKey);
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString("N");
            Session.SetString(SessionIdKey, sessionId);
        }
        return sessionId;
    }

    public InterestSignature GetInterestSignature()
    {
        var json = Session.GetString(InterestSignatureKey);
        if (string.IsNullOrEmpty(json))
        {
            return new InterestSignature();
        }

        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<InterestSignature>(json)
                   ?? new InterestSignature();
        }
        catch
        {
            return new InterestSignature();
        }
    }

    public void SaveInterestSignature(InterestSignature signature)
    {
        signature.LastUpdated = DateTime.UtcNow;
        var json = System.Text.Json.JsonSerializer.Serialize(signature);
        Session.SetString(InterestSignatureKey, json);
    }

    public void ClearInterestSignature()
    {
        Session.Remove(InterestSignatureKey);
    }
}
