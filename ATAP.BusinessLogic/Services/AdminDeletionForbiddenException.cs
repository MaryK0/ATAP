namespace ATAP.BusinessLogic.Services;

/// <summary>
/// Попытка удалить пользователя с ролью <c>admin</c> (FR-16). Удалять пользователей может только администратор,
/// поэтому запрет «удалить себя» — частный случай этого правила. Интерфейсы показывают сообщение,
/// REST API отвечает <c>403 Forbidden</c>.
/// </summary>
public sealed class AdminDeletionForbiddenException : InvalidOperationException
{
    public AdminDeletionForbiddenException()
        : base("Нельзя удалить пользователя с ролью admin")
    {
    }
}
