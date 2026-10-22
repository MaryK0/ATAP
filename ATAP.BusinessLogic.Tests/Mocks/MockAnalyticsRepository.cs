using ATAP.BusinessLogic.Interfaces.Repositories;

namespace ATAP.BusinessLogic.Tests.Mocks;

public class MockAnalyticsRepository : IAnalyticsRepository
{
    private readonly List<CongestionDb> _data;

    public MockAnalyticsRepository(bool empty = false)
    {
        if (!empty)
        {
            _data = new()
            {
                new() { Id = 1, AirportId = 1, FlightId = 1, IsDeparture = true, IsArrival = false },
                new() { Id = 2, AirportId = 1, FlightId = 2, IsDeparture = false, IsArrival = true }
            };
        }
        else
        {
            _data = new();
        }
    }

    public Task<List<CongestionDb>> GetCongestionAsync(int airportId, DateTime start, DateTime end, bool includeDetails = false)
    {
        var filtered = _data.Where(d => d.AirportId == airportId).ToList();
        return Task.FromResult(filtered);
    }

    public Task<List<CongestionDb>> GetAllCongestionAsync(DateTime start, DateTime end)
    {
        return Task.FromResult(new List<CongestionDb>(_data));
    }
}