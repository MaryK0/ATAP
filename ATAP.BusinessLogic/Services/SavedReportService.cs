using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BusinessLogic.Services;

public class SavedReportService : ISavedReportService
{
    private readonly ISavedReportRepository _repo;

    public SavedReportService(ISavedReportRepository repo)
    {
        _repo = repo;
    }

    public Task<List<SavedReportDto>> GetByUserAsync(int userId)
        => _repo.GetByUserAsync(userId);

    public async Task<int> SaveAsync(int userId, CreateSavedReportDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Название отчёта не может быть пустым");
        if (dto.DateFrom > dto.DateTo)
            throw new ArgumentException("Дата начала не может быть позже даты окончания");

        return await _repo.CreateAsync(userId, dto);
    }

    public Task DeleteAsync(int id, int userId)
        => _repo.DeleteAsync(id, userId);
}
