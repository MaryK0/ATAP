using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Services;

public interface ISavedReportService
{
    Task<List<SavedReportDto>> GetByUserAsync(int userId);
    Task<int> SaveAsync(int userId, CreateSavedReportDto dto);
    Task DeleteAsync(int id, int userId);
}
