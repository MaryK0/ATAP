using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Services;

public interface IMapService
{
    Task<List<FlightDto>> GetCurrentFlightsAsync();
    /// <summary>Рейсы, подходящие под фильтры карты (FR-06): область, высота, страна.</summary>
    Task<List<FlightDto>> GetCurrentFlightsAsync(FlightFilter filter);
    Task<List<AirportDto>> GetAirportsAsync();
}