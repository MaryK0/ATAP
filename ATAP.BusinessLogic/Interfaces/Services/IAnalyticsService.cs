using ATAP.BusinessLogic.DTO;

namespace ATAP.BusinessLogic.Interfaces.Services;

public interface IAnalyticsService
{
    Task<CongestionReportDto> GetCongestionAsync(int airportId, DateTime start, DateTime end);
    Task<byte[]> ExportToCsvAsync(CongestionReportDto report);
}