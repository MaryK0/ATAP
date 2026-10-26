namespace ATAP.Api.Dtos;

/// <summary>
/// Схема <c>CurrentFlight</c>: последняя позиция рейса. Необязательные поля контракта, которых нет в витрине,
/// не передаются; страна регистрации судна передаётся, когда известна.
/// </summary>
public sealed record CurrentFlightResponse(
    int FlightId, string Callsign, double Latitude, double Longitude, int? Altitude, string? OriginCountry);
