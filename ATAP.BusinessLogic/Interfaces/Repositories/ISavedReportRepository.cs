using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Repositories;

public interface ISavedReportRepository
{
    Task<List<SavedReportDto>> GetByUserAsync(int userId);
    Task<int> CreateAsync(int userId, CreateSavedReportDto dto);
    Task DeleteAsync(int id, int userId);
}
