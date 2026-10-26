namespace ATAP.BusinessLogic.DTO;

/// <summary>
/// Прямоугольник на карте в градусах (WGS 84). Если <see cref="MinLon"/> больше <see cref="MaxLon"/>,
/// область пересекает меридиан 180° (так бывает над Тихим океаном).
/// </summary>
public sealed record BoundingBox(double MinLon, double MinLat, double MaxLon, double MaxLat)
{
    /// <summary>Границы области входят в неё.</summary>
    public bool Contains(double latitude, double longitude)
    {
        if (latitude < MinLat || latitude > MaxLat)
            return false;

        return MinLon <= MaxLon
            ? longitude >= MinLon && longitude <= MaxLon
            : longitude >= MinLon || longitude <= MaxLon;
    }
}

/// <summary>
/// Фильтры карты (FR-06): видимая область, диапазон высот, страна регистрации судна.
/// Незаданный фильтр рейсы не ограничивает; заданные применяются вместе — рейс должен подходить под все.
/// </summary>
public sealed class FlightFilter
{
    /// <summary>Видимая область карты.</summary>
    public BoundingBox? Area { get; init; }

    /// <summary>Минимальная высота, м (включительно).</summary>
    public int? MinAltitude { get; init; }

    /// <summary>Максимальная высота, м (включительно).</summary>
    public int? MaxAltitude { get; init; }

    /// <summary>Страна регистрации судна (без учёта регистра).</summary>
    public string? Country { get; init; }

    public bool IsEmpty =>
        Area is null && MinAltitude is null && MaxAltitude is null && string.IsNullOrWhiteSpace(Country);

    public bool Matches(FlightDto flight)
    {
        ArgumentNullException.ThrowIfNull(flight);

        if (Area is not null && !Area.Contains(flight.Latitude, flight.Longitude))
            return false;
        if (MinAltitude is { } min && flight.Altitude < min)
            return false;
        if (MaxAltitude is { } max && flight.Altitude > max)
            return false;
        if (!string.IsNullOrWhiteSpace(Country)
            && !string.Equals(flight.Country, Country.Trim(), StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }

    public IEnumerable<FlightDto> Apply(IEnumerable<FlightDto> flights) => flights.Where(Matches);
}
