using ATAP.Api.Dtos;
using ATAP.Api.Infrastructure;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ATAP.Api.Controllers;

/// <summary>Справочник аэропортов (FR-05): доступен без входа.</summary>
[Route("api/v1/airports")]
[AllowAnonymous]
public sealed class AirportsController : ApiControllerBase
{
    private static readonly string[] AirportTypes = { "large", "medium", "small" };

    private readonly IMapService _map;

    public AirportsController(IMapService map) => _map = map;

    /// <summary>listAirports: страница аэропортов с фильтрами и поиском.</summary>
    [HttpGet]
    public async Task<IActionResult> ListAirports(
        [FromQuery] string? icaoCode, [FromQuery] string? type, [FromQuery] string? countryCode,
        [FromQuery] string? q, [FromQuery] int limit = 50, [FromQuery] int offset = 0)
    {
        var errors = new List<FieldError>();
        if (limit < 1 || limit > 10000) errors.Add(new("limit", "Допустимы значения от 1 до 10000"));
        if (offset < 0) errors.Add(new("offset", "Значение не может быть отрицательным"));
        if (type is not null && !AirportTypes.Contains(type)) errors.Add(new("type", "Допустимы large, medium, small"));
        if (errors.Count > 0) return BadData(errors.ToArray());

        IEnumerable<AirportDto> airports = await _map.GetAirportsAsync();
        if (icaoCode is not null) airports = airports.Where(a => a.IcaoCode.Equals(icaoCode, StringComparison.OrdinalIgnoreCase));
        if (type is not null) airports = airports.Where(a => a.Type == type);
        if (countryCode is not null) airports = airports.Where(a => string.Equals(a.CountryCode, countryCode, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(q))
            airports = airports.Where(a => Contains(a.Name, q) || Contains(a.City, q)
                                           || Contains(a.IcaoCode, q) || Contains(a.IataCode, q));

        var filtered = airports.OrderBy(a => a.Id).ToList();
        var page = filtered.Skip(offset).Take(limit).Select(ToResponse).ToList();
        return Ok(new PageResponse<AirportResponse>(page, filtered.Count, limit, offset));
    }

    /// <summary>getAirport: один аэропорт по идентификатору.</summary>
    [HttpGet("{airportId:int}")]
    public async Task<IActionResult> GetAirport(int airportId)
    {
        var airport = (await _map.GetAirportsAsync()).FirstOrDefault(a => a.Id == airportId);
        return airport is null
            ? ApiProblem(404, "not-found", "Аэропорт не найден")
            : Ok(ToResponse(airport));
    }

    private static bool Contains(string? value, string fragment) =>
        value?.Contains(fragment, StringComparison.OrdinalIgnoreCase) == true;

    internal static AirportResponse ToResponse(AirportDto a) => new(
        a.Id, a.IcaoCode, a.IataCode, a.Name, a.City, a.CountryCode,
        a.Latitude, a.Longitude, a.AnalysisRadius, a.Type, a.Elevation);
}
