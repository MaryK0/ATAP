using System.Text;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BlazorUI.ViewModels;

public class SavedReportViewModel
{
    private readonly ISavedReportService  _service;
    private readonly IAnalyticsRepository _analyticsRepo;

    public List<SavedReportDto> Reports    { get; private set; } = new();
    public bool                 IsLoading  { get; private set; }
    public string?              Error      { get; private set; }
    public string?              Success    { get; private set; }

    public SavedReportViewModel(ISavedReportService service, IAnalyticsRepository analyticsRepo)
    {
        _service       = service;
        _analyticsRepo = analyticsRepo;
    }

    public async Task LoadAsync(int userId)
    {
        IsLoading = true;
        Error     = null;
        try
        {
            Reports = await _service.GetByUserAsync(userId);
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task DeleteAsync(int reportId, int userId)
    {
        Success = null;
        Error   = null;
        try
        {
            await _service.DeleteAsync(reportId, userId);
            Reports.RemoveAll(r => r.Id == reportId);
            Success = "Отчёт удалён";
        }
        catch (Exception ex)
        {
            Error = ex.Message;
        }
    }

    public async Task<byte[]?> GetCsvBytesAsync(SavedReportDto r)
    {
        Error = null;
        try
        {
            var data = await _analyticsRepo.GetCongestionAsync(
                r.AirportId,
                r.DateFrom.ToDateTime(TimeOnly.MinValue),
                r.DateTo.ToDateTime(TimeOnly.MinValue).AddDays(1));

            var sb = new StringBuilder();
            sb.AppendLine("Id,AirportId,FlightId,Callsign,EntryTime,ExitTime,DurationSeconds,Type");
            foreach (var row in data)
                sb.AppendLine($"{row.Id},{row.AirportId},{row.FlightId},{row.Callsign}," +
                              $"{row.EntryTime:yyyy-MM-dd HH:mm:ss},{row.ExitTime:yyyy-MM-dd HH:mm:ss}," +
                              $"{row.DurationSeconds},{(row.IsDeparture ? "Departure" : "Arrival")}");
            return Encoding.UTF8.GetBytes(sb.ToString());
        }
        catch (Exception ex)
        {
            Error = $"Ошибка выгрузки: {ex.Message}";
            return null;
        }
    }
}
