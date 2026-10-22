using System.Text;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BusinessLogic.Services;

public class AnalyticsService : IAnalyticsService
{
    private readonly IAnalyticsRepository _analyticsRepo;
    private readonly ICacheRepository? _cacheRepo;

    public AnalyticsService(IAnalyticsRepository analyticsRepo, ICacheRepository? cacheRepo = null)
    {
        _analyticsRepo = analyticsRepo;
        _cacheRepo = cacheRepo;
    }

    public async Task<CongestionReportDto> GetCongestionAsync(int airportId, DateTime start, DateTime end)
    {
        if (start > end)
            throw new ArgumentException("Дата начала не может быть позже даты окончания", nameof(start));

        var cacheKey = $"congestion:{airportId}:{start:yyyyMMddHH}:{end:yyyyMMddHH}";

        if (_cacheRepo != null)
        {
            var cached = await _cacheRepo.GetAsync<CongestionReportDto>(cacheKey);
            if (cached != null)
                return cached;
        }

        var rows = await _analyticsRepo.GetCongestionAsync(airportId, start, end, includeDetails: true);

        var report = new CongestionReportDto
        {
            AirportId = airportId,
            PeriodStart = start,
            PeriodEnd = end,
            DepartureCount = rows.Count(r => r.IsDeparture),
            ArrivalCount = rows.Count(r => r.IsArrival),
            Entries = rows.Select(r => new CongestionEntryDto
            {
                Id = r.Id,
                FlightId = r.FlightId,
                Callsign = r.Callsign,
                EntryTime = r.EntryTime,
                ExitTime = r.ExitTime,
                DurationSeconds = r.DurationSeconds,
                IsDeparture = r.IsDeparture,
                IsArrival = r.IsArrival,
                MaxAltitudeInZone = r.MaxAltitudeInZone,
                MinAltitudeInZone = r.MinAltitudeInZone
            }).ToList()
        };

        if (_cacheRepo != null)
            await _cacheRepo.SetAsync(cacheKey, report, 3600);

        return report;
    }

    public Task<byte[]> ExportToCsvAsync(CongestionReportDto report)
    {
        if (report == null)
            throw new ArgumentNullException(nameof(report));

        var sb = new StringBuilder();
        sb.AppendLine("FlightId,Callsign,EntryTime,ExitTime,DurationSeconds,IsDeparture,IsArrival,MaxAltitude,MinAltitude");

        foreach (var e in report.Entries)
        {
            sb.AppendLine(
                $"{e.FlightId}," +
                $"{e.Callsign}," +
                $"{e.EntryTime:yyyy-MM-dd HH:mm:ss}," +
                $"{e.ExitTime:yyyy-MM-dd HH:mm:ss}," +
                $"{e.DurationSeconds}," +
                $"{e.IsDeparture}," +
                $"{e.IsArrival}," +
                $"{e.MaxAltitudeInZone}," +
                $"{e.MinAltitudeInZone}");
        }

        return Task.FromResult(Encoding.UTF8.GetBytes(sb.ToString()));
    }
}
