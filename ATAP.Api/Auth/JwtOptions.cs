using System.Text;

namespace ATAP.Api.Auth;

public sealed class JwtOptions
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "atap-api";
    public string Audience { get; set; } = "atap-clients";
    public int ExpiresMinutes { get; set; } = 60;
    public int LifetimeSeconds => checked(ExpiresMinutes * 60);

    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32 || string.IsNullOrWhiteSpace(Issuer)
            || string.IsNullOrWhiteSpace(Audience) || ExpiresMinutes is < 1 or > 1440)
            throw new InvalidOperationException("Configure a JWT key of at least 32 bytes, issuer, audience and a lifetime of 1–1440 minutes.");
    }
}
