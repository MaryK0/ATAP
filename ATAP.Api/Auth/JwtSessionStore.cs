using System.Collections.Concurrent;

namespace ATAP.Api.Auth;

/// <summary>Tracks active JWT IDs so logout/password changes take effect immediately.</summary>
public sealed class JwtSessionStore(TimeProvider clock)
{
    private sealed record Session(int UserId, DateTimeOffset ExpiresAt);
    private readonly ConcurrentDictionary<string, Session> _sessions = new();

    public void Register(string jti, int userId, DateTimeOffset expiresAt)
    {
        foreach (var item in _sessions)
            if (item.Value.ExpiresAt <= clock.GetUtcNow()) _sessions.TryRemove(item.Key, out _);
        _sessions[jti] = new(userId, expiresAt);
    }

    public bool IsActive(string jti, int userId)
    {
        if (!_sessions.TryGetValue(jti, out var session) || session.UserId != userId) return false;
        if (session.ExpiresAt > clock.GetUtcNow()) return true;
        _sessions.TryRemove(jti, out _);
        return false;
    }

    public void Revoke(string jti) => _sessions.TryRemove(jti, out _);
    public void RevokeUser(int userId)
    {
        foreach (var item in _sessions)
            if (item.Value.UserId == userId) _sessions.TryRemove(item.Key, out _);
    }
}
