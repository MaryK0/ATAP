namespace ATAP.BlazorUI.Services;

public class AppStateService
{
    public event Action? OnChange;

    private bool    _isAuthenticated;
    private string? _currentUser;
    private string? _userRole;
    private int     _userId;

    public bool    IsAuthenticated => _isAuthenticated;
    public string? CurrentUser     => _currentUser;
    public string? UserRole        => _userRole;
    public int     UserId          => _userId;

    public void SetUser(bool isAuthenticated, string? username = null, string? role = null, int userId = 0)
    {
        _isAuthenticated = isAuthenticated;
        _currentUser     = username;
        _userRole        = role;
        _userId          = userId;
        OnChange?.Invoke();
    }

    public void Logout()
    {
        _isAuthenticated = false;
        _currentUser     = null;
        _userRole        = null;
        _userId          = 0;
        OnChange?.Invoke();
    }
}