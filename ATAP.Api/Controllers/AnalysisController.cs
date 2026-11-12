using System.ComponentModel.DataAnnotations;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

[Route("api/v1"), Authorize(Roles = "analyst,admin")]
public sealed class AnalysisController(PlatformService service) : ApiControllerBase
{
    [HttpGet("congestion")]
    public async Task<IActionResult> Congestion(int? airportId, [Required] DateOnly? dateFrom, [Required] DateOnly? dateTo, int limit = 50, int offset = 0, CancellationToken ct = default) =>
        await Analysis(airportId, new(dateFrom!.Value, dateTo!.Value), new(limit, offset), ct);
    private async Task<IActionResult> Analysis(int? airportId, AnalysisPeriod period, Paging paging, CancellationToken ct)
    {
        var format = NegotiateJsonOrCsv(); if (format == NegotiatedFormat.NotAcceptable) return NotAcceptable();
        paging.Validate(); Response.Headers.Vary = "Accept";
        if (format == NegotiatedFormat.Csv)
            return File(await service.ExportCsvAsync(airportId, period, ct), "text/csv; charset=utf-8", $"congestion_{airportId?.ToString() ?? "all"}_{period.From:yyyy-MM-dd}_{period.To:yyyy-MM-dd}.csv");
        return Ok((await service.CongestionAsync(airportId, period, paging, ct)).ToApi());
    }
    [HttpGet("reports")]
    public async Task<IActionResult> Reports(int limit = 50, int offset = 0, CancellationToken ct = default) =>
        Ok((await service.ReportsAsync(CurrentUserId, new(limit, offset), ct)).ToApi(r => r.ToApi()));
    [HttpPost("reports"), Consumes("application/json")]
    public async Task<IActionResult> CreateReport(ReportCreateRequest request, CancellationToken ct)
    {
        if (request.AirportId is null || request.DateFrom is null || request.DateTo is null || request.Name is null)
            return BadData(new FieldError("body", "Укажите name, airportId, dateFrom и dateTo"));
        var report = await service.CreateReportAsync(CurrentUserId, request.Name, request.AirportId.Value, new(request.DateFrom.Value, request.DateTo.Value), ct);
        Response.Headers.Location = $"/api/v1/reports/{report.Id}"; return StatusCode(201, report.ToApi());
    }
    [HttpGet("reports/{reportId:int}")]
    public async Task<IActionResult> Report([Range(1, int.MaxValue)] int reportId, CancellationToken ct) => Ok((await service.ReportAsync(reportId, CurrentUserId, ct)).ToApi());
    [HttpDelete("reports/{reportId:int}")]
    public async Task<IActionResult> DeleteReport([Range(1, int.MaxValue)] int reportId, CancellationToken ct)
    { await service.DeleteReportAsync(reportId, CurrentUserId, ct); return NoContent(); }
    [HttpGet("reports/{reportId:int}/congestion")]
    public async Task<IActionResult> ReportCongestion([Range(1, int.MaxValue)] int reportId, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        var report = await service.ReportAsync(reportId, CurrentUserId, ct);
        return await Analysis(report.AirportId, new(report.DateFrom, report.DateTo), new(limit, offset), ct);
    }
}
