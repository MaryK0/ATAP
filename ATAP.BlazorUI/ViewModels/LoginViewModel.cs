using ATAP.BlazorUI.Services;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BlazorUI.ViewModels;

public class LoginViewModel
{
    private readonly IAuthService _auth;
    private readonly AppStateService _appState;

    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string? Error { get; private set; }
    public bool IsLoading { get; private set; }

    public LoginViewModel(IAuthService auth, AppStateService appState)
    {
        _auth = auth;
        _appState = appState;
    }

    public async Task<UserDto?> LoginAsync()
    {
        Error = null;
        if (string.IsNullOrWhiteSpace(Username)) { Error = "Введите логин"; return null; }
        if (string.IsNullOrWhiteSpace(Password)) { Error = "Введите пароль"; return null; }

        IsLoading = true;
        try
        {
            var user = await _auth.LoginAsync(Username, Password);
            if (user == null) Error = "Неверный логин или пароль";
            return user;
        }
        catch (Exception ex)
        {
            Error = $"Ошибка подключения: {ex.Message}";
            return null;
        }
        finally
        {
            IsLoading = false;
        }
    }
}
