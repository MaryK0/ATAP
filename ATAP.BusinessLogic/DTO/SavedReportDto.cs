namespace ATAP.BusinessLogic.DTO;

public class SavedReportDto
{
    public int Id { get; set; }
    public string Name { get; set; } = "";
    public int AirportId { get; set; }
    public string AirportName { get; set; } = "";
    public DateOnly DateFrom { get; set; }
    public DateOnly DateTo { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateSavedReportDto
{
    public string   Name      { get; set; } = "";
    public int      AirportId { get; set; }
    public DateOnly DateFrom  { get; set; }
    public DateOnly DateTo    { get; set; }
}
