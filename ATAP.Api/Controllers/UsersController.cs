using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ATAP.Api.Auth;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

[Route("api/v1/users")]
[Authorize(Roles = "admin")]
public sealed class UsersController(PlatformService service, JwtSessionStore tokens) : ApiControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(string? role, int limit = 50, int offset = 0, CancellationToken ct = default) =>
        Ok((await service.UsersAsync(role, new(limit, offset), ct)).ToApi(u => u.ToApi()));
    [HttpGet("{userId:int}")]
    public async Task<IActionResult> Get([Range(1, int.MaxValue)] int userId, CancellationToken ct) => Ok((await service.UserAsync(userId, ct)).ToApi());
    [HttpPost, AllowAnonymous, Consumes("application/json")]
    public async Task<IActionResult> Create(UserCreateRequest request, CancellationToken ct)
    {
        var authenticated = User.Identity?.IsAuthenticated == true;
        // AllowAnonymous skips authorization, not authentication. A supplied invalid credential is not silently treated as a guest.
        if (Request.Headers.ContainsKey("Authorization") && !authenticated)
        { Response.Headers.WWWAuthenticate = "Bearer"; return ApiProblem(401, "unauthorized", "Требуется действительный токен"); }
        if (authenticated && !User.IsInRole("admin")) return ApiProblem(403, "forbidden", "Создавать пользователей может только администратор");
        if (!authenticated && request.Role is not null) return ApiProblem(403, "forbidden", "При регистрации нельзя задавать роль");
        var user = await service.CreateUserAsync(request.Username, request.Email, request.Password, request.Role ?? "analyst", ct);
        if (authenticated) Response.Headers.Location = $"/api/v1/users/{user.Id}";
        return StatusCode(201, user.ToApi());
    }
    [HttpPut("{userId:int}"), Consumes("application/json")]
    public async Task<IActionResult> Replace([Range(1, int.MaxValue)] int userId, UserReplaceRequest request, CancellationToken ct) =>
        Ok((await service.UpdateUserAsync(userId, request.Username, request.Email, request.Role, null, ct)).ToApi());
    [HttpPatch("{userId:int}"), Consumes("application/merge-patch+json")]
    public async Task<IActionResult> Patch([Range(1, int.MaxValue)] int userId, [FromBody] JsonElement patch, CancellationToken ct)
    {
        if (patch.ValueKind != JsonValueKind.Object) return BadData(new FieldError("body", "Ожидается JSON-объект"));
        var names = new HashSet<string>();
        foreach (var field in patch.EnumerateObject())
            if (!names.Add(field.Name) || field.Name is not ("username" or "email" or "role" or "password") || field.Value.ValueKind != JsonValueKind.String)
                return BadData(new FieldError(field.Name, "Неизвестное поле, повторное поле или значение не является строкой"));
        if (names.Count == 0) return BadData(new FieldError("body", "Укажите хотя бы одно поле"));
        string? Value(string name) => patch.TryGetProperty(name, out var value) ? value.GetString() : null;
        var user = await service.UpdateUserAsync(userId, Value("username"), Value("email"), Value("role"), Value("password"), ct);
        if (names.Contains("password")) tokens.RevokeUser(userId);
        return Ok(user.ToApi());
    }
    [HttpDelete("{userId:int}")]
    public async Task<IActionResult> Delete([Range(1, int.MaxValue)] int userId, CancellationToken ct)
    { await service.DeleteUserAsync(userId, ct); tokens.RevokeUser(userId); return NoContent(); }
}
