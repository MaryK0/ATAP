using System.Globalization;
using System.Text;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

/// <summary>Витрина загруженности аэропортов (FR-07…FR-10): JSON или CSV по заголовку Accept.</summary>
[Route("api/v1/congestion")]
[Authorize(Roles = "analyst,admin")]
public sealed class CongestionController : ApiControllerBase
{
    private const string CsvHeader =
        "id,airportId,flightId,callsign,entryTime,exitTime,durationSeconds,isDeparture,isArrival,maxAltitudeInZone,minAltitudeInZone";

    private readonly IAnalyticsService _analytics;
    private readonly IMapService _map;

    public CongestionController(IAnalyticsService analytics, IMapService map)
    {
        _analytics = analytics;
        _map = map;
    }

    /// <summary>getCongestion: записи витрины за период и счётчики вылетов и прилётов.</summary>
    [HttpGet]
    public async Task<IActionResult> GetCongestion(
        [FromQuery] int? airportId, [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var format = NegotiateJsonOrCsv();
        if (format == NegotiatedFormat.NotAcceptable) return NotAcceptable();

        var errors = new List<FieldError>();
        if (dateFrom is null) errors.Add(new("dateFrom", "Укажите дату начала периода"));
        if (dateTo is null) errors.Add(new("dateTo", "Укажите дату окончания периода"));
        if (airportId is < 1) errors.Add(new("airportId", "Идентификатор должен быть положительным"));
        if (limit < 1 || limit > 1000) errors.Add(new("limit", "Допустимы значения от 1 до 1000"));
        if (offset < 0) errors.Add(new("offset", "Значение не может быть отрицательным"));
        if (errors.Count > 0) return BadData(errors.ToArray());

        var periodError = PeriodRules.Validate(dateFrom!.Value, dateTo!.Value);
        if (periodError is { } p) return ApiProblem(400, p.Slug, p.Title);

        string airportCode = "all";
        if (airportId is { } id)
        {
            var airport = (await _map.GetAirportsAsync()).FirstOrDefault(a => a.Id == id);
            if (airport is null)
                return ApiProblem(400, "unknown-airport", "Аэропорт не найден",
                    "Параметр airportId указывает на несуществующий аэропорт");
            airportCode = airport.IcaoCode;
        }

        var (start, end) = PeriodRules.ToInterval(dateFrom.Value, dateTo.Value);
        var report = airportId is { } known
            ? await _analytics.GetCongestionAsync(known, start, end)
            : await _analytics.GetAllCongestionAsync(start, end);

        var entries = report.Entries.OrderByDescending(e => e.EntryTime).ToList();

        if (format == NegotiatedFormat.Csv)
        {
            var fileName = $"congestion_{airportCode}_{dateFrom:yyyy-MM-dd}_{dateTo:yyyy-MM-dd}.csv";
            return File(Encoding.UTF8.GetBytes(BuildCsv(entries)), "text/csv; charset=utf-8", fileName);
        }

        var page = entries.Skip(offset).Take(limit).Select(ToResponse).ToList();
        return Ok(new CongestionReportResponse(
            airportId, dateFrom.Value, dateTo.Value,
            new CongestionSummary(report.DepartureCount, report.ArrivalCount),
            page, entries.Count, limit, offset));
    }

    private static CongestionEntryResponse ToResponse(CongestionEntryDto e) => new(
        e.Id, e.AirportId, e.FlightId, e.Callsign, Utc(e.EntryTime), Utc(e.ExitTime),
        e.DurationSeconds, e.IsDeparture, e.IsArrival, e.MaxAltitudeInZone, e.MinAltitudeInZone);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static string BuildCsv(IEnumerable<CongestionEntryDto> entries)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder(CsvHeader).Append('\n');
        foreach (var e in entries)
        {
            sb.Append(e.Id.ToString(inv)).Append(',')
              .Append(e.AirportId.ToString(inv)).Append(',')
              .Append(e.FlightId.ToString(inv)).Append(',')
              .Append(Escape(e.Callsign)).Append(',')
              .Append(Utc(e.EntryTime).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv)).Append(',')
              .Append(Utc(e.ExitTime).ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", inv)).Append(',')
              .Append(e.DurationSeconds.ToString(inv)).Append(',')
              .Append(e.IsDeparture ? "true" : "false").Append(',')
              .Append(e.IsArrival ? "true" : "false").Append(',')
              .Append(e.MaxAltitudeInZone.ToString(inv)).Append(',')
              .Append(e.MinAltitudeInZone.ToString(inv)).Append('\n');
        }
        return sb.ToString();
    }

    private static string Escape(string value) =>
        value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
