using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ATAP.BusinessLogic.DTO;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace ATAP.Api.Auth;

/// <summary>Выпуск токенов доступа: идентификатор пользователя (sub), роль (role), идентификатор токена (jti).</summary>
public sealed class JwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
        SigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
    }

    public SymmetricSecurityKey SigningKey { get; }

    public string Issuer => _options.Issuer;

    public (string Token, int ExpiresInSeconds) Create(UserDto user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_options.ExpiresMinutes);
        var claims = new List<Claim>
        {
            new("sub", user.UserId.ToString()),
            new("name", user.Username),
            new("role", user.Role),
            new("jti", Guid.NewGuid().ToString("N"))
        };
        var credentials = new SigningCredentials(SigningKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(_options.Issuer, _options.Issuer, claims, now, expires, credentials);
        return (new JwtSecurityTokenHandler().WriteToken(token), _options.ExpiresMinutes * 60);
    }
}
