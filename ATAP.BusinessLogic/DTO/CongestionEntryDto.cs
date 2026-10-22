namespace ATAP.BusinessLogic.DTO;

public class CongestionEntryDto
{
    public long Id { get; set; }
    public int FlightId { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public DateTime ExitTime { get; set; }
    public int DurationSeconds { get; set; }
    public bool IsDeparture { get; set; }
    public bool IsArrival { get; set; }
    public int MaxAltitudeInZone { get; set; }
    public int MinAltitudeInZone { get; set; }
}
