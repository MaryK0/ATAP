using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Services;

public class MapService : IMapService
{
    private readonly IMapRepository _mapRepository;
    private readonly ICacheRepository? _cacheRepository;

    public MapService(IMapRepository mapRepository, ICacheRepository? cacheRepository = null)
    {
        _mapRepository = mapRepository;
        _cacheRepository = cacheRepository;
    }

    public async Task<List<FlightDto>> GetCurrentFlightsAsync()
    {
        var flights = await _mapRepository.GetCurrentFlightsAsync();
        
        return flights.Select(f => new FlightDto
        {
            Id = f.FlightId,
            Callsign = f.Callsign,
            Latitude = f.Latitude,
            Longitude = f.Longitude,
            Altitude = f.Altitude
        }).ToList();
    }

    public async Task<List<AirportDto>> GetAirportsAsync()
    {
        const string cacheKey = "airports:all";
        
        if (_cacheRepository != null)
        {
            var cached = await _cacheRepository.GetAsync<List<AirportDto>>(cacheKey);
            if (cached != null)
                return cached;
        }

        var airports = await _mapRepository.GetAirportsAsync();
        
        var result = airports.Select(a => new AirportDto
        {
            Id = a.AirportId,
            Name = a.Name,
            IcaoCode = a.IcaoCode,
            Latitude = a.Latitude,
            Longitude = a.Longitude
        }).ToList();

        if (_cacheRepository != null)
        {
            await _cacheRepository.SetAsync(cacheKey, result, 3600);
        }

        return result;
    }

    // ← ДОБАВИТЬ ЭТИ МЕТОДЫ
    public async Task InvalidateAirportsCacheAsync()
    {
        if (_cacheRepository != null)
        {
            await _cacheRepository.RemoveAsync("airports:all");
        }
    }

    public async Task InvalidateAircraftsCacheAsync()
    {
        if (_cacheRepository != null)
        {
            await _cacheRepository.RemoveAsync("aircrafts:all");
        }
    }
}