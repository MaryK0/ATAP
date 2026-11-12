using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using ATAP.BusinessLogic.Domain;
using Microsoft.IdentityModel.Tokens;

namespace ATAP.Api.Auth;

public sealed class JwtTokenService(JwtOptions options, JwtSessionStore sessions, TimeProvider clock)
{
    public int LifetimeSeconds => options.LifetimeSeconds;

    public string Issue(User user)
    {
        var now = clock.GetUtcNow();
        var expires = now.AddSeconds(LifetimeSeconds);
        var jti = Guid.NewGuid().ToString("N");
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString(CultureInfo.InvariantCulture)),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture), ClaimValueTypes.Integer64),
            new Claim("name", user.Username),
            new Claim("role", user.Role)
        };
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256);
        var jwt = new JwtSecurityToken(options.Issuer, options.Audience, claims,
            now.UtcDateTime, expires.UtcDateTime, credentials);
        sessions.Register(jti, user.Id, expires);
        return new JwtSecurityTokenHandler().WriteToken(jwt);
    }
}
