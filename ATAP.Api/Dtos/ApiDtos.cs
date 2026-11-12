using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using D = ATAP.BusinessLogic.Domain;

namespace ATAP.Api.Dtos;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class LoginRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Username { get; set; } = "";
    [Required] public string Password { get; set; } = "";
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UserCreateRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Username { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
    [RegularExpression("^(analyst|admin)$")] public string? Role { get; set; }
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class UserReplaceRequest
{
    [Required, StringLength(50, MinimumLength = 1)] public string Username { get; set; } = "";
    [Required, EmailAddress, StringLength(100)] public string Email { get; set; } = "";
    [Required, RegularExpression("^(analyst|admin)$")] public string Role { get; set; } = "";
}
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class AnalysisRequest
{
    [Range(1, int.MaxValue)] public int? AirportId { get; set; }
    [Required] public DateOnly? DateFrom { get; set; }
    [Required] public DateOnly? DateTo { get; set; }
}
public sealed record AircraftResponse(int Id, string Icao24, string? Registration, string? Model, string? Manufacturer, string? OriginCountry);
public sealed record FlightResponse(int Id, string Callsign, int? AircraftId, DateTime? FirstSeen, DateTime? LastSeen);
public sealed record PositionResponse(long Id, int FlightId, DateTime TimePosition, double Latitude, double Longitude, int? Altitude, double? Velocity, int? Heading, bool? OnGround);
public sealed record CurrentFlightResponse(int FlightId, string Icao24, string Callsign, string? OriginCountry, string? AircraftModel, double Latitude, double Longitude, int? Altitude, double? Velocity, int? Heading, bool? OnGround);
public sealed record ExportResponse(Guid Id, string Status, int? AirportId, DateOnly DateFrom, DateOnly DateTo, DateTime CreatedAt, string? DownloadUrl, string? Error);

public static class ApiMapping
{
    public static UserResponse ToApi(this D.User u) => new(u.Id, u.Username, u.Email, u.Role, u.CreatedAt);
    public static AirportResponse ToApi(this D.Airport a) => new(a.Id, a.IcaoCode, a.IataCode, a.Name, a.City, a.CountryCode, a.Latitude, a.Longitude, a.AnalysisRadius, a.Type, a.Elevation);
    public static AircraftResponse ToApi(this D.Aircraft a) => new(a.Id, a.Icao24, a.Registration, a.Model, a.Manufacturer, a.OriginCountry);
    public static FlightResponse ToApi(this D.Flight f) => new(f.Id, f.Callsign, f.AircraftId, f.FirstSeen, f.LastSeen);
    public static PositionResponse ToApi(this D.Position p) => new(p.Id, p.FlightId, p.TimePosition, p.Latitude, p.Longitude, p.Altitude, p.Velocity, p.Heading, p.OnGround);
    public static CurrentFlightResponse ToApi(this D.CurrentFlight f) => new(f.FlightId, f.Icao24, f.Callsign, f.OriginCountry, f.AircraftModel, f.Latitude, f.Longitude, f.Altitude, f.Velocity, f.Heading, f.OnGround);
    public static CongestionEntryResponse ToApi(this D.CongestionEntry e) => new(e.Id, e.AirportId, e.FlightId, e.Callsign, e.EntryTime, e.ExitTime, e.DurationSeconds, e.IsDeparture, e.IsArrival, e.MaxAltitudeInZone, e.MinAltitudeInZone);
    public static ReportResponse ToApi(this D.SavedReport r) => new(r.Id, r.Name, r.AirportId, r.AirportName, r.DateFrom, r.DateTo, r.CreatedAt);
    public static PageResponse<TOut> ToApi<T, TOut>(this D.Page<T> page, Func<T, TOut> map) => new(page.Items.Select(map).ToList(), page.Total, page.Limit, page.Offset);
    public static CongestionReportResponse ToApi(this D.CongestionResult r) => new(r.AirportId, r.DateFrom, r.DateTo, new(r.Departures, r.Arrivals), r.Page.Items.Select(e => e.ToApi()).ToList(), r.Page.Total, r.Page.Limit, r.Page.Offset);
}
