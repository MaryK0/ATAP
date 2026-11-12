namespace ATAP.BusinessLogic.Domain;

// Domain models have no database or HTTP dependencies. API DTOs and database rows are separate.
public sealed record User(int Id, string Username, string Email, string Role, DateTime CreatedAt);
public sealed record UserAccount(User User, string PasswordHash);
public sealed record Aircraft(int Id, string Icao24, string? Registration, string? Model,
    string? Manufacturer, string? OriginCountry);
public sealed record Flight(int Id, string Callsign, int? AircraftId, DateTime? FirstSeen, DateTime? LastSeen);
public sealed record Position(long Id, int FlightId, DateTime TimePosition, double Latitude, double Longitude,
    int? Altitude, double? Velocity, int? Heading, bool? OnGround);
public sealed record CurrentFlight(int FlightId, string Icao24, string Callsign, string? OriginCountry,
    string? AircraftModel, double Latitude, double Longitude, int? Altitude, double? Velocity,
    int? Heading, bool? OnGround);
public sealed record Airport(int Id, string IcaoCode, string? IataCode, string Name, string? City,
    string? CountryCode, double Latitude, double Longitude, int AnalysisRadius, string? Type, int? Elevation);
public sealed record CongestionEntry(long Id, int AirportId, int FlightId, string Callsign,
    DateTime EntryTime, DateTime ExitTime, int DurationSeconds, bool IsDeparture, bool IsArrival,
    int? MaxAltitudeInZone, int? MinAltitudeInZone);
public sealed record SavedReport(int Id, int UserId, string Name, int AirportId, string AirportName,
    DateOnly DateFrom, DateOnly DateTo, DateTime CreatedAt);
public sealed record Page<T>(IReadOnlyList<T> Items, int Total, int Limit, int Offset);
public sealed record CongestionResult(int? AirportId, DateOnly DateFrom, DateOnly DateTo,
    Page<CongestionEntry> Page, int Departures, int Arrivals);

public sealed record Paging(int Limit = 50, int Offset = 0)
{
    public void Validate(int maximum = 1000)
    {
        if (Limit < 1 || Limit > maximum || Offset < 0)
            throw new DomainException(DomainError.Invalid, "validation-error", "Некорректные параметры пагинации");
    }
}

public sealed record AnalysisPeriod(DateOnly From, DateOnly To)
{
    public void Validate()
    {
        if (From > To)
            throw new DomainException(DomainError.Invalid, "invalid-period", "Дата начала не может быть позже даты окончания");
        if (To.DayNumber - From.DayNumber + 1 > 31)
            throw new DomainException(DomainError.Invalid, "period-too-long", "Период не может быть больше 31 дня");
    }
    public DateTime Start => From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
    public DateTime EndExclusive => To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}

public sealed record AirportFilter(string? IcaoCode = null, string? Type = null,
    string? CountryCode = null, string? Q = null);
public sealed record AircraftFilter(string? Icao24 = null, string? OriginCountry = null);
public sealed record FlightQuery(string? Callsign = null, int? AircraftId = null,
    DateTime? SeenFrom = null, DateTime? SeenTo = null);
public sealed record PositionQuery(DateTime? From = null, DateTime? To = null);
public sealed record MapQuery(double? MinLon = null, double? MinLat = null, double? MaxLon = null,
    double? MaxLat = null, int? MinAltitude = null, int? MaxAltitude = null, string? OriginCountry = null);

public enum DomainError { Invalid, NotFound, Forbidden, Conflict, Unavailable }
public sealed class DomainException(DomainError error, string code, string message) : Exception(message)
{
    public DomainError Error { get; } = error;
    public string Code { get; } = code;
}
