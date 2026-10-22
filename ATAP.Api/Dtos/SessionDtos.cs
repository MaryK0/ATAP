using System.ComponentModel.DataAnnotations;

namespace ATAP.Api.Dtos;

/// <summary>Схема <c>SessionCreate</c>: логин и пароль для входа.</summary>
public sealed class SessionCreateRequest
{
    [Required, StringLength(50, MinimumLength = 1)]
    public string? Username { get; set; }

    [Required, MinLength(1)]
    public string? Password { get; set; }
}

/// <summary>Схема <c>User</c> (без пароля и его хэша).</summary>
public sealed record UserResponse(int Id, string Username, string Email, string Role, DateTime CreatedAt);

/// <summary>Схема <c>Session</c>: токен доступа и пользователь.</summary>
public sealed record SessionResponse(string AccessToken, string TokenType, int ExpiresIn, UserResponse User);
