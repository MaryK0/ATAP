namespace ATAP.BusinessLogic.DTO;

public class AirportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IcaoCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int AnalysisRadius { get; set; } = 10000;
    public string? IataCode { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? Type { get; set; }
    public int? Elevation { get; set; }
}