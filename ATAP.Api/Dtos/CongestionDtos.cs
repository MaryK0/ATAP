namespace ATAP.Api.Dtos;

/// <summary>Схема <c>CongestionEntry</c>.</summary>
public sealed record CongestionEntryResponse(
    long Id,
    int AirportId,
    int FlightId,
    string Callsign,
    DateTime EntryTime,
    DateTime ExitTime,
    int DurationSeconds,
    bool IsDeparture,
    bool IsArrival,
    int? MaxAltitudeInZone,
    int? MinAltitudeInZone);

public sealed record CongestionSummary(int Departures, int Arrivals);

/// <summary>Схема <c>CongestionReport</c>: сводка за весь период и страница записей.</summary>
public sealed record CongestionReportResponse(
    int? AirportId,
    DateOnly DateFrom,
    DateOnly DateTo,
    CongestionSummary Summary,
    IReadOnlyList<CongestionEntryResponse> Items,
    int Total,
    int Limit,
    int Offset);
