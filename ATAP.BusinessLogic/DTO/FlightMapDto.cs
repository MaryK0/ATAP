namespace ATAP.BusinessLogic.DTO;

public class FlightDto
{
    public int Id { get; set; }
    public string Callsign { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Altitude { get; set; }
}