using ATAP.Api.Auth;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

[Route("api/v1")]
public sealed class AuthController(PlatformService service, JwtTokenService tokens, JwtSessionStore sessions) : ApiControllerBase
{
    [HttpPost("login"), AllowAnonymous, Consumes("application/json")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await service.LoginAsync(request.Username!, request.Password!, ct);
        if (user is null)
        { Response.Headers.WWWAuthenticate = "Bearer"; return ApiProblem(401, "invalid-credentials", "Неверный логин или пароль"); }
        Response.Headers.CacheControl = "no-store";
        return StatusCode(201, new SessionResponse(tokens.Issue(user), "Bearer", tokens.LifetimeSeconds, user.ToApi()));
    }
    [HttpPost("logout"), Authorize(Roles = "analyst,admin")]
    public IActionResult Logout()
    { sessions.Revoke(User.FindFirst("jti")!.Value); return NoContent(); }
}
