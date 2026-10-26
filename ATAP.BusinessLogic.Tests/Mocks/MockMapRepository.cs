using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.Mocks;

public class MockMapRepository : IMapRepository
{
    private readonly List<FlightMapDb> _flights;
    private readonly List<AirportDb> _airports;

    public MockMapRepository(bool empty = false)
    {
        if (!empty)
        {
            _flights = new()
            {
                new() { FlightId = 1, Callsign = "AFL123", Latitude = 55.75, Longitude = 37.62, Altitude = 10000, Country = "Russia" },
                new() { FlightId = 2, Callsign = "SBI456", Latitude = 55.76, Longitude = 37.63, Altitude = 11000, Country = "Russia" }
            };
            _airports = new()
            {
                new() { AirportId = 1, Name = "Шереметьево", IcaoCode = "UUEE", Latitude = 55.972, Longitude = 37.415 },
                new() { AirportId = 2, Name = "Домодедово", IcaoCode = "UUDD", Latitude = 55.409, Longitude = 37.902 }
            };
        }
        else
        {
            _flights = new();
            _airports = new();
        }
    }

    public Task<List<FlightMapDb>> GetCurrentFlightsAsync() => Task.FromResult(_flights);
    public Task<List<AirportDb>> GetAirportsAsync() => Task.FromResult(_airports);
}