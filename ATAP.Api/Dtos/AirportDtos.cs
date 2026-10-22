namespace ATAP.Api.Dtos;

/// <summary>Схема <c>Airport</c>.</summary>
public sealed record AirportResponse(
    int Id,
    string IcaoCode,
    string? IataCode,
    string Name,
    string? City,
    string? CountryCode,
    double Latitude,
    double Longitude,
    int AnalysisRadius,
    string? Type,
    int? Elevation);

/// <summary>Страница коллекции: <c>items</c> плюс <c>total</c>, <c>limit</c>, <c>offset</c> (схема <c>PageMeta</c>).</summary>
public sealed record PageResponse<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);
