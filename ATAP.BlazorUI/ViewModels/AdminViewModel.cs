using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BlazorUI.ViewModels;

public class AdminViewModel
{
    private readonly IAdminService _adminService;
    private readonly IAuthService  _authService;

    public List<UserDto> Users      { get; private set; } = new();
    public bool          IsLoading  { get; private set; }
    public string?       Error      { get; private set; }

    // Modal state
    public bool    ShowModal   { get; private set; }
    public bool    IsEditing   { get; private set; }
    public UserDto EditUser    { get; set; } = new();
    public string  NewPassword { get; set; } = "";
    public string? ModalError  { get; private set; }

    public int AdminCount   => Users.Count(u => u.Role == "admin");
    public int AnalystCount => Users.Count(u => u.Role == "analyst");

    public AdminViewModel(IAdminService adminService, IAuthService authService)
    {
        _adminService = adminService;
        _authService  = authService;
    }

    public async Task LoadAsync()
    {
        IsLoading = true;
        Error = null;
        try { Users = await _adminService.GetAllUsersAsync(); }
        catch (Exception ex) { Error = $"Ошибка загрузки: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    public void OpenAddForm()
    {
        IsEditing  = false;
        EditUser   = new UserDto { Username = "", Email = "", Role = "analyst" };
        NewPassword = "";
        ModalError  = null;
        ShowModal   = true;
    }

    public void OpenEditForm(UserDto user)
    {
        IsEditing  = true;
        EditUser   = new UserDto { UserId = user.UserId, Username = user.Username, Email = user.Email, Role = user.Role };
        NewPassword = "";
        ModalError  = null;
        ShowModal   = true;
    }

    public void CloseModal() => ShowModal = false;

    public async Task<bool> SaveAsync()
    {
        ModalError = null;
        if (string.IsNullOrWhiteSpace(EditUser.Username)) { ModalError = "Введите логин"; return false; }
        if (string.IsNullOrWhiteSpace(EditUser.Email))    { ModalError = "Введите email"; return false; }
        if (!IsEditing && string.IsNullOrWhiteSpace(NewPassword)) { ModalError = "Введите пароль"; return false; }

        try
        {
            if (IsEditing)
            {
                var ok = await _adminService.UpdateUserAsync(
                    EditUser.UserId, EditUser.Username, EditUser.Email,
                    string.IsNullOrWhiteSpace(NewPassword) ? null : NewPassword,
                    EditUser.Role);
                if (!ok) { ModalError = "Не удалось обновить пользователя"; return false; }
            }
            else
            {
                var ok = await _authService.RegisterAsync(EditUser.Username, EditUser.Email, NewPassword);
                if (!ok) { ModalError = "Логин или email уже заняты"; return false; }

                if (EditUser.Role == "admin")
                {
                    var all = await _adminService.GetAllUsersAsync();
                    var created = all.FirstOrDefault(u => u.Username == EditUser.Username);
                    if (created != null)
                        await _adminService.UpdateUserAsync(created.UserId, created.Username, created.Email, null, "admin");
                }
            }

            ShowModal = false;
            await LoadAsync();
            return true;
        }
        catch (Exception ex)
        {
            ModalError = $"Ошибка: {ex.Message}";
            return false;
        }
    }

    public async Task<bool> DeleteAsync(int userId, int currentUserId)
    {
        if (userId == currentUserId) { Error = "Нельзя удалить свой аккаунт"; return false; }
        try
        {
            await _adminService.DeleteUserAsync(userId);
            await LoadAsync();
            return true;
        }
        catch (Exception ex)
        {
            Error = $"Ошибка удаления: {ex.Message}";
            return false;
        }
    }
}
