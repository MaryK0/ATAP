using System.Text;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BlazorUI.ViewModels;

public class AnalyticsViewModel
{
    private readonly IAnalyticsRepository _analyticsRepo;
    private readonly IMapRepository       _mapRepo;
    private readonly ISavedReportService  _savedReportService;

    public List<AirportDto>   Airports           { get; private set; } = new();
    public int                SelectedAirportId  { get; set; }
    public DateTime           StartDate          { get; set; } = DateTime.Today.AddYears(-1);
    public DateTime           EndDate            { get; set; } = DateTime.Today;
    public List<CongestionDb> Data               { get; private set; } = new();
    public bool               IsLoading          { get; private set; }
    public string?            Error              { get; private set; }

    public string  SaveName    { get; set; } = "";
    public string? SaveError   { get; private set; }
    public string? SaveSuccess { get; private set; }
    public bool    IsSaving    { get; private set; }

    public int DepartureCount => Data.Count(x => x.IsDeparture);
    public int ArrivalCount   => Data.Count(x => x.IsArrival);

    public AnalyticsViewModel(IAnalyticsRepository analyticsRepo, IMapRepository mapRepo, ISavedReportService savedReportService)
    {
        _analyticsRepo      = analyticsRepo;
        _mapRepo            = mapRepo;
        _savedReportService = savedReportService;
    }

    public async Task InitAsync()
    {
        await LoadAirportsAsync();
        await ApplyFiltersAsync();
    }

    public async Task LoadAirportsAsync()
    {
        try
        {
            var raw = await _mapRepo.GetAirportsAsync();
            Airports = raw.Select(a => new AirportDto
            {
                Id       = a.AirportId,
                Name     = a.Name,
                IcaoCode = a.IcaoCode,
                Latitude = a.Latitude,
                Longitude = a.Longitude
            }).ToList();
        }
        catch (Exception ex)
        {
            Error = $"Ошибка загрузки аэропортов: {ex.Message}";
        }
    }

    public async Task ApplyFiltersAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            if (StartDate > EndDate)
            {
                Error = "Дата начала не может быть позже даты окончания";
                Data = new();
                return;
            }

            var all = await _analyticsRepo.GetAllCongestionAsync(StartDate, EndDate);
            Data = SelectedAirportId == 0
                ? all
                : all.Where(x => x.AirportId == SelectedAirportId).ToList();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            Data = new();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<bool> SaveReportAsync(int userId)
    {
        SaveError   = null;
        SaveSuccess = null;
        IsSaving    = true;
        try
        {
            await _savedReportService.SaveAsync(userId, new CreateSavedReportDto
            {
                Name      = SaveName,
                AirportId = SelectedAirportId,
                DateFrom  = DateOnly.FromDateTime(StartDate),
                DateTo    = DateOnly.FromDateTime(EndDate)
            });
            SaveSuccess = $"Отчёт «{SaveName}» сохранён";
            SaveName    = "";
            return true;
        }
        catch (ArgumentException ex)
        {
            SaveError = ex.Message;
            return false;
        }
        catch (Exception ex)
        {
            SaveError = $"Ошибка: {ex.Message}";
            return false;
        }
        finally
        {
            IsSaving = false;
        }
    }

    public byte[] BuildCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Id,AirportId,FlightId,Callsign,EntryTime,ExitTime,DurationSeconds,Type");
        foreach (var row in Data)
            sb.AppendLine($"{row.Id},{row.AirportId},{row.FlightId},{row.Callsign}," +
                          $"{row.EntryTime:yyyy-MM-dd HH:mm:ss},{row.ExitTime:yyyy-MM-dd HH:mm:ss}," +
                          $"{row.DurationSeconds},{(row.IsDeparture ? "Departure" : "Arrival")}");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
