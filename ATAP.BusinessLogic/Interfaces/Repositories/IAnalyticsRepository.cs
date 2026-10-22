namespace ATAP.BusinessLogic.Interfaces.Repositories;

public interface IAnalyticsRepository
{
    Task<List<CongestionDb>> GetCongestionAsync(int airportId, DateTime start, DateTime end, bool includeDetails = false);
    Task<List<CongestionDb>> GetAllCongestionAsync(DateTime start, DateTime end);
}

public class CongestionDb
{
    public long Id { get; set; }
    public int AirportId { get; set; }
    public int FlightId { get; set; }
    public bool IsDeparture { get; set; }
    public bool IsArrival { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime DateHour { get; set; }
    public int MaxAltitudeInZone { get; set; }
    public int MinAltitudeInZone { get; set; }
    public DateTime LoadedAt { get; set; }
}