using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.Mocks;

/// <summary>
/// Классический (без mock-фреймворка) in-memory fake для ISavedReportRepository.
/// Хранит состояние в списке и ведёт себя как реальный репозиторий с точки зрения вызывающего кода.
/// </summary>
public class MockSavedReportRepository : ISavedReportRepository
{
    private readonly List<(int Id, int UserId, CreateSavedReportDto Dto)> _reports = new();
    private int _nextId = 1;

    public Task<List<SavedReportDto>> GetByUserAsync(int userId)
    {
        var result = _reports
            .Where(r => r.UserId == userId)
            .Select(r => new SavedReportDto
            {
                Id = r.Id,
                Name = r.Dto.Name,
                AirportId = r.Dto.AirportId,
                AirportName = $"Airport#{r.Dto.AirportId}",
                DateFrom = r.Dto.DateFrom,
                DateTo = r.Dto.DateTo,
                CreatedAt = DateTime.UtcNow
            })
            .ToList();

        return Task.FromResult(result);
    }

    public Task<int> CreateAsync(int userId, CreateSavedReportDto dto)
    {
        var id = _nextId++;
        _reports.Add((id, userId, dto));
        return Task.FromResult(id);
    }

    public Task DeleteAsync(int id, int userId)
    {
        _reports.RemoveAll(r => r.Id == id && r.UserId == userId);
        return Task.CompletedTask;
    }

    /// <summary>Помогает Arrange-секции теста проверить, что запись действительно физически сохранена.</summary>
    public bool Contains(int userId, string name) => _reports.Any(r => r.UserId == userId && r.Dto.Name == name);

    public int CountForUser(int userId) => _reports.Count(r => r.UserId == userId);
}
