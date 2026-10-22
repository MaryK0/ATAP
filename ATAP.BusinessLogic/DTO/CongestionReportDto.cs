namespace ATAP.BusinessLogic.DTO;

public class CongestionReportDto
{
    public int AirportId { get; set; }
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public int DepartureCount { get; set; }
    public int ArrivalCount { get; set; }
    public List<CongestionEntryDto> Entries { get; set; } = new();
}
