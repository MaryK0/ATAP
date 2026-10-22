using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.BlazorUI.ViewModels;

public class MapViewModel
{
    private readonly IMapService _mapService;

    public int     FlightCount { get; private set; }
    public bool    IsLoading   { get; private set; }
    public string? Error       { get; private set; }

    public MapViewModel(IMapService mapService)
    {
        _mapService = mapService;
    }

    public async Task<IReadOnlyList<FlightMapPoint>> LoadFlightsAsync()
    {
        IsLoading = true;
        Error = null;
        try
        {
            var flights = await _mapService.GetCurrentFlightsAsync();
            FlightCount = flights.Count;
            return flights
                .Where(f => f.Latitude != 0 || f.Longitude != 0)
                .Select(f => new FlightMapPoint
                {
                    Callsign  = f.Callsign,
                    Latitude  = f.Latitude,
                    Longitude = f.Longitude,
                    Altitude  = f.Altitude
                })
                .ToList();
        }
        catch (Exception ex)
        {
            Error = ex.Message;
            return Array.Empty<FlightMapPoint>();
        }
        finally
        {
            IsLoading = false;
        }
    }

    public async Task<IReadOnlyList<AirportMapPoint>> LoadAirportsAsync()
    {
        try
        {
            var airports = await _mapService.GetAirportsAsync();
            return airports
                .Where(a => a.Latitude != 0 || a.Longitude != 0)
                .Select(a => new AirportMapPoint
                {
                    Name      = a.Name,
                    IcaoCode  = a.IcaoCode,
                    Latitude  = a.Latitude,
                    Longitude = a.Longitude
                })
                .ToList();
        }
        catch
        {
            return Array.Empty<AirportMapPoint>();
        }
    }
}

public record AirportMapPoint
{
    public string Name      { get; init; } = "";
    public string IcaoCode  { get; init; } = "";
    public double Latitude  { get; init; }
    public double Longitude { get; init; }
}
