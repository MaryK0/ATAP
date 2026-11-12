using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

[Route("api/v1/exports"), Authorize(Roles = "analyst,admin")]
public sealed class ExportsController(ExportService service) : ApiControllerBase
{
    [HttpPost, Consumes("application/json")]
    public async Task<IActionResult> Create(AnalysisRequest request, CancellationToken ct)
    {
        var job = await service.CreateAsync(CurrentUserId, request.AirportId, new(request.DateFrom!.Value, request.DateTo!.Value), ct);
        Response.Headers.Location = $"/api/v1/exports/{job.Id}"; Response.Headers.RetryAfter = "1";
        return StatusCode(202, ToApi(job));
    }
    [HttpGet("{exportId:guid}")]
    public async Task<IActionResult> Get(Guid exportId, CancellationToken ct)
    {
        var job = await service.GetAsync(exportId, CurrentUserId, ct); Response.Headers.CacheControl = "no-store";
        if (job.Status is "queued" or "running") Response.Headers.RetryAfter = "1";
        return Ok(ToApi(job));
    }
    [HttpGet("{exportId:guid}/file")]
    public async Task<IActionResult> Download(Guid exportId, CancellationToken ct)
    {
        var accepted = Request.GetTypedHeaders().Accept;
        if (accepted is not null && accepted.Count > 0 && !accepted.Any(a => a.Quality != 0 && (a.MediaType == "text/csv" || a.MediaType == "text/*" || a.MediaType == "*/*"))) return NotAcceptable();
        return File(await service.FileAsync(exportId, CurrentUserId, ct), "text/csv; charset=utf-8", $"export_{exportId}.csv");
    }
    private static ExportResponse ToApi(ReportExport job) => new(job.Id, job.Status, job.AirportId, job.DateFrom, job.DateTo, job.CreatedAt,
        job.Status == "completed" ? $"/api/v1/exports/{job.Id}/file" : null, job.Error);
}
