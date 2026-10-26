using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

/// <summary>Текущие позиции рейсов для карты (FR-04, FR-06): снимок без пагинации, доступен без входа.</summary>
[Route("api/v1/flights")]
[AllowAnonymous]
public sealed class FlightsController : ApiControllerBase
{
    private readonly IMapService _map;

    public FlightsController(IMapService map) => _map = map;

    /// <summary>
    /// listCurrentFlights: снимок позиций. Фильтры карты (FR-06) необязательны и применяются вместе:
    /// <c>bbox</c> — видимая область, <c>minAltitude</c>/<c>maxAltitude</c> — высота, <c>originCountry</c> — страна судна.
    /// </summary>
    [HttpGet("current")]
    public async Task<IActionResult> ListCurrentFlights(
        [FromQuery] string? bbox, [FromQuery] int? minAltitude, [FromQuery] int? maxAltitude,
        [FromQuery] string? originCountry)
    {
        var errors = new List<FieldError>();

        BoundingBox? area = null;
        if (bbox is not null)
        {
            area = ParseBbox(bbox);
            if (area is null)
                errors.Add(new("bbox",
                    "Ожидаются четыре числа: minLon,minLat,maxLon,maxLat (долгота от -180 до 180, широта от -90 до 90, minLat не больше maxLat)"));
        }
        if (minAltitude is < 0) errors.Add(new("minAltitude", "Высота не может быть отрицательной"));
        if (maxAltitude is < 0) errors.Add(new("maxAltitude", "Высота не может быть отрицательной"));
        if (minAltitude > maxAltitude) errors.Add(new("minAltitude", "Минимальная высота не может быть больше максимальной"));
        if (originCountry is { Length: > 100 }) errors.Add(new("originCountry", "Не более 100 символов"));
        if (errors.Count > 0) return BadData(errors.ToArray());

        var filter = new FlightFilter
        {
            Area = area,
            MinAltitude = minAltitude,
            MaxAltitude = maxAltitude,
            Country = originCountry
        };

        var flights = await _map.GetCurrentFlightsAsync(filter);
        var body = flights.OrderBy(f => f.Id)
            .Select(f => new CurrentFlightResponse(f.Id, f.Callsign, f.Latitude, f.Longitude, f.Altitude, f.Country))
            .ToList();

        // Условный запрос: клиент опрашивает снимок раз в 5 минут и не перекачивает неизменившийся
        var etag = "\"" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body)))[..8]).ToLowerInvariant() + "\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = "max-age=300";
        if (Request.Headers.IfNoneMatch.Contains(etag))
            return StatusCode(StatusCodes.Status304NotModified);

        return Ok(body);
    }

    private static BoundingBox? ParseBbox(string value)
    {
        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4) return null;
        var numbers = new double[4];
        for (var i = 0; i < 4; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out numbers[i])
                || double.IsNaN(numbers[i]) || double.IsInfinity(numbers[i]))
                return null;

        var (minLon, minLat, maxLon, maxLat) = (numbers[0], numbers[1], numbers[2], numbers[3]);
        if (minLon is < -180 or > 180 || maxLon is < -180 or > 180) return null;
        if (minLat is < -90 or > 90 || maxLat is < -90 or > 90) return null;
        if (minLat > maxLat) return null;
        return new BoundingBox(minLon, minLat, maxLon, maxLat);
    }
}
