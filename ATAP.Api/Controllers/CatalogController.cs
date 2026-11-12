using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

[Route("api/v1")]
public sealed class CatalogController(PlatformService service) : ApiControllerBase
{
    [HttpGet("airports"), AllowAnonymous]
    public async Task<IActionResult> Airports(string? icaoCode, string? type, string? countryCode, string? q,
        int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        if (type is not null && type is not ("large" or "medium" or "small")) return BadData(new FieldError("type", "Допустимы large, medium, small"));
        if (icaoCode is not null && icaoCode.Length != 4) return BadData(new FieldError("icaoCode", "Код ICAO содержит 4 символа"));
        if (countryCode is not null && countryCode.Length != 2) return BadData(new FieldError("countryCode", "Код страны содержит 2 символа"));
        if (q is not null && (string.IsNullOrWhiteSpace(q) || q.Length > 100)) return BadData(new FieldError("q", "От 1 до 100 символов"));
        return Ok((await service.AirportsAsync(new(icaoCode, type, countryCode, q), new(limit, offset), ct)).ToApi(a => a.ToApi()));
    }
    [HttpGet("airports/{airportId:int}"), AllowAnonymous]
    public async Task<IActionResult> Airport([Range(1, int.MaxValue)] int airportId, CancellationToken ct) => Ok((await service.AirportAsync(airportId, ct)).ToApi());
    [HttpGet("aircraft"), Authorize(Roles = "analyst,admin")]
    public async Task<IActionResult> Aircraft(string? icao24, string? originCountry, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        if (icao24 is not null && !Regex.IsMatch(icao24, "^[A-F0-9]{6}$")) return BadData(new FieldError("icao24", "Ожидаются 6 шестнадцатеричных символов в верхнем регистре"));
        if (originCountry?.Length > 100) return BadData(new FieldError("originCountry", "Не более 100 символов"));
        return Ok((await service.AircraftAsync(new(icao24, originCountry), new(limit, offset), ct)).ToApi(a => a.ToApi()));
    }
    [HttpGet("aircraft/{aircraftId:int}"), Authorize(Roles = "analyst,admin")]
    public async Task<IActionResult> AircraftById([Range(1, int.MaxValue)] int aircraftId, CancellationToken ct) => Ok((await service.AircraftAsync(aircraftId, ct)).ToApi());
    [HttpGet("flights"), Authorize(Roles = "analyst,admin")]
    public async Task<IActionResult> Flights(string? callsign, [Range(1, int.MaxValue)] int? aircraftId,
        DateTimeOffset? seenFrom, DateTimeOffset? seenTo, int limit = 50, int offset = 0, CancellationToken ct = default)
    {
        if (callsign?.Length > 10) return BadData(new FieldError("callsign", "Не более 10 символов"));
        return Ok((await service.FlightsAsync(new(callsign, aircraftId, seenFrom?.UtcDateTime, seenTo?.UtcDateTime), new(limit, offset), ct)).ToApi(f => f.ToApi()));
    }
    [HttpGet("flights/{flightId:int}"), Authorize(Roles = "analyst,admin")]
    public async Task<IActionResult> Flight([Range(1, int.MaxValue)] int flightId, CancellationToken ct) => Ok((await service.FlightAsync(flightId, ct)).ToApi());
    [HttpGet("flights/{flightId:int}/positions"), Authorize(Roles = "analyst,admin")]
    public async Task<IActionResult> Positions([Range(1, int.MaxValue)] int flightId, int limit = 50, int offset = 0, CancellationToken ct = default) =>
        Ok((await service.PositionsAsync(flightId, new(), new(limit, offset), ct)).ToApi(p => p.ToApi()));
    [HttpGet("flights/current"), AllowAnonymous]
    public async Task<IActionResult> Current(string? bbox, int? minAltitude, int? maxAltitude, string? originCountry, CancellationToken ct)
    {
        double? minLon = null, minLat = null, maxLon = null, maxLat = null;
        if (bbox is not null)
        {
            var parts = bbox.Split(','); var values = new double[4];
            if (parts.Length != 4) return BadData(new FieldError("bbox", "Ожидаются minLon,minLat,maxLon,maxLat"));
            for (var i = 0; i < 4; i++)
                if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i]) || !double.IsFinite(values[i]))
                    return BadData(new FieldError("bbox", "Координаты должны быть числами"));
            if (values[0] is < -180 or > 180 || values[2] is < -180 or > 180 || values[1] is < -90 or > 90 || values[3] is < -90 or > 90 || values[1] > values[3])
                return BadData(new FieldError("bbox", "Координаты вне диапазона или minLat больше maxLat"));
            (minLon, minLat, maxLon, maxLat) = (values[0], values[1], values[2], values[3]);
        }
        if (minAltitude is < 0 || maxAltitude is < 0 || minAltitude > maxAltitude) return BadData(new FieldError("minAltitude", "Некорректный диапазон высот"));
        if (originCountry?.Length > 100) return BadData(new FieldError("originCountry", "Не более 100 символов"));
        var body = (await service.CurrentFlightsAsync(new(minLon, minLat, maxLon, maxLat, minAltitude, maxAltitude, originCountry), ct)).Select(f => f.ToApi()).ToList();
        var etag = "\"" + Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(body))).ToLowerInvariant() + "\"";
        Response.Headers.ETag = etag; Response.Headers.CacheControl = "max-age=300";
        if (Request.GetTypedHeaders().IfNoneMatch?.Any(t => t.Tag == etag || t.Tag == "*") == true) return StatusCode(304);
        return Ok(body);
    }
}
