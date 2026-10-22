namespace ATAP.Api.Dtos;

/// <summary>Схема <c>ReportCreate</c>. Поля необязательные на уровне привязки: проверка и сообщения — в контроллере.</summary>
public sealed class ReportCreateRequest
{
    public string? Name { get; set; }
    public int? AirportId { get; set; }
    public DateOnly? DateFrom { get; set; }
    public DateOnly? DateTo { get; set; }
}

/// <summary>Схема <c>Report</c>.</summary>
public sealed record ReportResponse(
    int Id,
    string Name,
    int AirportId,
    string AirportName,
    DateOnly DateFrom,
    DateOnly DateTo,
    DateTime CreatedAt);
