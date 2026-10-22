using System.Collections.Concurrent;

namespace ATAP.Api.Auth;

/// <summary>
/// Отозванные токены (выход из системы): идентификатор токена хранится до конца его срока действия.
/// Хранилище в памяти процесса — для среза API; перед несколькими экземплярами сервера его нужно
/// заменить общим хранилищем.
/// </summary>
public sealed class RevokedTokenStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _revoked = new();

    public void Revoke(string jti, DateTimeOffset expiresAt)
    {
        _revoked[jti] = expiresAt;
        Purge();
    }

    public bool IsRevoked(string jti) => _revoked.ContainsKey(jti);

    private void Purge()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var (jti, expiresAt) in _revoked)
            if (expiresAt < now)
                _revoked.TryRemove(jti, out _);
    }
}
