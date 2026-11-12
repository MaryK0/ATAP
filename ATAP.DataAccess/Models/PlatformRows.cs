using ATAP.BusinessLogic.Domain;

namespace ATAP.DataAccess.Models;

// Database row models use database names; mapping to the domain is explicit.
internal sealed record UserRow(int UserId, string Username, string Email, string PasswordHash, string Role, DateTime CreatedAt)
{
    public UserAccount ToDomain() => new(new(UserId, Username, Email, Role, CreatedAt), PasswordHash);
}
internal sealed record AircraftRow(int AircraftId, string Icao24, string? Registration, string? Model, string? Manufacturer, string? OriginCountry)
{
    public Aircraft ToDomain() => new(AircraftId, Icao24, Registration, Model, Manufacturer, OriginCountry);
}
internal sealed record FlightRow(int FlightId, string Callsign, int? AircraftId, DateTime? FirstSeen, DateTime? LastSeen)
{
    public Flight ToDomain() => new(FlightId, Callsign, AircraftId, FirstSeen, LastSeen);
}
internal sealed record AirportRow(int AirportId, string IcaoCode, string? IataCode, string Name, string? City,
    string? CountryCode, double Latitude, double Longitude, int AnalysisRadius, string? AirportType, int? Elevation)
{
    public Airport ToDomain() => new(AirportId, IcaoCode, IataCode, Name, City, CountryCode, Latitude, Longitude, AnalysisRadius, AirportType, Elevation);
}
internal sealed record PositionRow(long PositionId, int FlightId, DateTime TimePosition, double Latitude, double Longitude,
    int? Altitude, double? Velocity, int? Heading, bool? OnGround)
{
    public Position ToDomain() => new(PositionId, FlightId, TimePosition, Latitude, Longitude, Altitude, Velocity, Heading, OnGround);
}
internal sealed record MapRow(int FlightId, string Icao24, string Callsign, string? OriginCountry, string? AircraftModel,
    double Latitude, double Longitude, int? Altitude, double? Velocity, int? TrueTrack, bool? OnGround)
{
    public CurrentFlight ToDomain() => new(FlightId, Icao24, Callsign, OriginCountry, AircraftModel,
        Latitude, Longitude, Altitude, Velocity, TrueTrack, OnGround);
}
internal sealed record CongestionRow(long Id, int AirportId, int FlightId, string Callsign, DateTime EntryTime,
    DateTime ExitTime, int DurationSeconds, bool IsDeparture, bool IsArrival, int? MaxAltitudeInZone, int? MinAltitudeInZone)
{
    public CongestionEntry ToDomain() => new(Id, AirportId, FlightId, Callsign, EntryTime, ExitTime,
        DurationSeconds, IsDeparture, IsArrival, MaxAltitudeInZone, MinAltitudeInZone);
}
internal sealed record ReportRow(int Id, int UserId, string Name, int AirportId, string AirportName,
    DateOnly DateFrom, DateOnly DateTo, DateTime CreatedAt)
{
    public SavedReport ToDomain() => new(Id, UserId, Name, AirportId, AirportName, DateFrom, DateTo, CreatedAt);
}
