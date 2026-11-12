namespace ATAP.BusinessLogic.Domain;

public sealed record ReportExport(Guid Id, int UserId, int? AirportId, DateOnly DateFrom, DateOnly DateTo,
    string Status, DateTime CreatedAt, byte[]? Result, string? Error);

public interface IExportRepository
{
    Task<ReportExport> CreateAsync(int userId, int? airportId, AnalysisPeriod period, CancellationToken ct);
    Task<ReportExport?> GetAsync(Guid id, int userId, CancellationToken ct);
    Task<ReportExport?> ClaimAsync(CancellationToken ct);
    Task CompleteAsync(Guid id, byte[]? result, string? error, CancellationToken ct);
}

public sealed class ExportService(IExportRepository repository, PlatformService platform)
{
    public async Task<ReportExport> CreateAsync(int userId, int? airportId, AnalysisPeriod period, CancellationToken ct)
    { await platform.ValidateAnalysisAsync(airportId, period, ct); return await repository.CreateAsync(userId, airportId, period, ct); }
    public async Task<ReportExport> GetAsync(Guid id, int userId, CancellationToken ct) =>
        await repository.GetAsync(id, userId, ct) ?? throw new DomainException(DomainError.NotFound, "not-found", "Задание не найдено");
    public async Task<byte[]> FileAsync(Guid id, int userId, CancellationToken ct)
    {
        var export = await GetAsync(id, userId, ct);
        if (export.Status != "completed" || export.Result is null)
            throw new DomainException(DomainError.Conflict, "export-not-ready", "Файл ещё не готов или задание завершилось ошибкой");
        return export.Result;
    }
}
