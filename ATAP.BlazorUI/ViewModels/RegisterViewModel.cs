using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BlazorUI.ViewModels;

public class RegisterViewModel
{
    private readonly IAuthService _auth;

    public string Username { get; set; } = "";
    public string Email { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }
    public bool Success { get; private set; }

    public RegisterViewModel(IAuthService auth)
    {
        _auth = auth;
    }

    public async Task<bool> RegisterAsync()
    {
        Error = null;
        Success = false;
        if (string.IsNullOrWhiteSpace(Username)) { Error = "Введите логин"; return false; }
        if (string.IsNullOrWhiteSpace(Email))    { Error = "Введите email"; return false; }
        if (string.IsNullOrWhiteSpace(Password)) { Error = "Введите пароль"; return false; }

        IsLoading = true;
        try
        {
            var ok = await _auth.RegisterAsync(Username, Email, Password);
            if (ok) { Success = true; return true; }
            Error = "Пользователь с таким логином или email уже существует";
            return false;
        }
        catch (Exception ex)
        {
            Error = $"Ошибка: {ex.Message}";
            return false;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
