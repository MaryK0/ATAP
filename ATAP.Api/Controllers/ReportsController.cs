using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

/// <summary>Сохранённые отчёты текущего пользователя (FR-11, FR-12). Владелец определяется по токену.</summary>
[Route("api/v1/reports")]
[Authorize(Roles = "analyst,admin")]
public sealed class ReportsController : ApiControllerBase
{
    private readonly ISavedReportService _reports;
    private readonly IMapService _map;

    public ReportsController(ISavedReportService reports, IMapService map)
    {
        _reports = reports;
        _map = map;
    }

    /// <summary>listReports: отчёты текущего пользователя, сначала новые.</summary>
    [HttpGet]
    public async Task<IActionResult> ListReports([FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var errors = new List<FieldError>();
        if (limit < 1 || limit > 1000) errors.Add(new("limit", "Допустимы значения от 1 до 1000"));
        if (offset < 0) errors.Add(new("offset", "Значение не может быть отрицательным"));
        if (errors.Count > 0) return BadData(errors.ToArray());

        var all = await _reports.GetByUserAsync(CurrentUserId);
        var page = all.Skip(offset).Take(limit).Select(ToResponse).ToList();
        return Ok(new PageResponse<ReportResponse>(page, all.Count, limit, offset));
    }

    /// <summary>createReport: сохраняет имя, аэропорт и период отчёта.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateReport([FromBody] ReportCreateRequest request)
    {
        var errors = new List<FieldError>();
        if (string.IsNullOrWhiteSpace(request.Name)) errors.Add(new("name", "Укажите название отчёта"));
        else if (request.Name.Length > 100) errors.Add(new("name", "Не более 100 символов"));
        if (request.AirportId is null or < 1) errors.Add(new("airportId", "Выберите аэропорт"));
        if (request.DateFrom is null) errors.Add(new("dateFrom", "Укажите дату начала периода"));
        if (request.DateTo is null) errors.Add(new("dateTo", "Укажите дату окончания периода"));
        if (errors.Count > 0) return BadData(errors.ToArray());

        var periodError = PeriodRules.Validate(request.DateFrom!.Value, request.DateTo!.Value);
        if (periodError is { } p) return ApiProblem(400, p.Slug, p.Title);

        var airports = await _map.GetAirportsAsync();
        if (airports.All(a => a.Id != request.AirportId))
            return ApiProblem(400, "unknown-airport", "Аэропорт не найден",
                "Поле airportId указывает на несуществующий аэропорт");

        var id = await _reports.SaveAsync(CurrentUserId, new CreateSavedReportDto
        {
            Name = request.Name!.Trim(),
            AirportId = request.AirportId!.Value,
            DateFrom = request.DateFrom.Value,
            DateTo = request.DateTo.Value
        });

        var created = (await _reports.GetByUserAsync(CurrentUserId)).First(r => r.Id == id);
        Response.Headers.Location = $"/api/v1/reports/{id}";
        return StatusCode(StatusCodes.Status201Created, ToResponse(created));
    }

    /// <summary>getReport: один отчёт; чужой отчёт считается несуществующим.</summary>
    [HttpGet("{reportId:int}")]
    public async Task<IActionResult> GetReport(int reportId)
    {
        var report = (await _reports.GetByUserAsync(CurrentUserId)).FirstOrDefault(r => r.Id == reportId);
        return report is null ? ReportNotFound() : Ok(ToResponse(report));
    }

    /// <summary>deleteReport: удаляет отчёт текущего пользователя.</summary>
    [HttpDelete("{reportId:int}")]
    public async Task<IActionResult> DeleteReport(int reportId)
    {
        var report = (await _reports.GetByUserAsync(CurrentUserId)).FirstOrDefault(r => r.Id == reportId);
        if (report is null) return ReportNotFound();

        await _reports.DeleteAsync(reportId, CurrentUserId);
        return NoContent();
    }

    private IActionResult ReportNotFound() => ApiProblem(404, "not-found", "Отчёт не найден");

    private static ReportResponse ToResponse(SavedReportDto r) => new(
        r.Id, r.Name, r.AirportId, r.AirportName, r.DateFrom, r.DateTo,
        DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc));
}
