namespace ATAP.BusinessLogic.DTO;

public class AirportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IcaoCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}