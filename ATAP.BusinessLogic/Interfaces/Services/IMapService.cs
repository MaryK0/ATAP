using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Services;

public interface IMapService
{
    Task<List<FlightDto>> GetCurrentFlightsAsync();
    Task<List<AirportDto>> GetAirportsAsync();
}