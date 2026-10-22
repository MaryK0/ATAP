namespace ATAP.ConsoleUI;

public class AppSettings
{
    /// <summary>
    /// Максимальное кол-во рейсов, выводимых в таблице консольного UI.
    /// </summary>
    public int MaxFlightsPreview { get; set; } = 30;

    /// <summary>
    /// Максимальное кол-во строк отчёта, выводимых на экран (все строки попадают в CSV).
    /// </summary>
    public int MaxReportPreview { get; set; } = 50;
}
