namespace ATAP.Api.Auth;

/// <summary>Настройки JWT; ключ в рабочем окружении задаётся переменной Jwt__Key.</summary>
public sealed class JwtOptions
{
    public string Key { get; set; } = "dev-only-signing-key-change-in-real-deployments-0123456789";
    public string Issuer { get; set; } = "atap-api";
    public int ExpiresMinutes { get; set; } = 60;
}
