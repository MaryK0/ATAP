namespace ATAP.DataAccess;

public class CacheSettings
{
    public int AirportsTTLSeconds { get; set; } = 86400;  // 24 часа
    public int AircraftsTTLSeconds { get; set; } = 86400; // 24 часа
}