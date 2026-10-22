using ATAP.Api.Auth;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

/// <summary>Вход и выход (FR-02, FR-03): сессия — ресурс <c>/api/v1/sessions</c>.</summary>
[Route("api/v1/sessions")]
public sealed class SessionsController : ApiControllerBase
{
    private readonly IAuthService _auth;
    private readonly JwtTokenService _tokens;
    private readonly RevokedTokenStore _revoked;

    public SessionsController(IAuthService auth, JwtTokenService tokens, RevokedTokenStore revoked)
    {
        _auth = auth;
        _tokens = tokens;
        _revoked = revoked;
    }

    /// <summary>createSession: проверяет логин и пароль, возвращает токен доступа.</summary>
    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> CreateSession([FromBody] SessionCreateRequest request)
    {
        var user = await _auth.LoginAsync(request.Username!, request.Password!);
        if (user is null)
        {
            // Неизвестный логин и неверный пароль неотличимы (сценарий 2)
            Response.Headers.WWWAuthenticate = "Bearer";
            return ApiProblem(401, "invalid-credentials", "Неверный логин или пароль");
        }

        var (token, expiresIn) = _tokens.Create(user);
        Response.Headers.Location = "/api/v1/sessions/current";
        return StatusCode(StatusCodes.Status201Created,
            new SessionResponse(token, "Bearer", expiresIn, ToResponse(user)));
    }

    /// <summary>deleteCurrentSession: отзывает токен вызывающего.</summary>
    [HttpDelete("current")]
    [Authorize(Roles = "analyst,admin")]
    public IActionResult DeleteCurrentSession()
    {
        var jti = User.FindFirst("jti")?.Value;
        var exp = User.FindFirst("exp")?.Value;
        if (jti is not null)
        {
            var expiresAt = exp is not null && long.TryParse(exp, out var seconds)
                ? DateTimeOffset.FromUnixTimeSeconds(seconds)
                : DateTimeOffset.UtcNow.AddHours(1);
            _revoked.Revoke(jti, expiresAt);
        }
        return NoContent();
    }

    private static UserResponse ToResponse(UserDto user) =>
        new(user.UserId, user.Username, user.Email, user.Role, DateTime.SpecifyKind(user.CreatedAt, DateTimeKind.Utc));
}
