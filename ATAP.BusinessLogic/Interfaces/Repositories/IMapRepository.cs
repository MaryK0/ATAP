namespace ATAP.BusinessLogic.Interfaces.Repositories;

public interface IMapRepository
{
    Task<List<FlightMapDb>> GetCurrentFlightsAsync();
    Task<List<AirportDb>> GetAirportsAsync();
}

public class FlightMapDb
{
    public int FlightId { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Altitude { get; set; }
}

public class AirportDb
{
    public int AirportId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IcaoCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int AnalysisRadius { get; set; } = 10000;
    public string? IataCode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? AirportType { get; set; }
    public int? Elevation { get; set; }
}