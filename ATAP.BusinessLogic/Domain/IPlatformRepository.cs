namespace ATAP.BusinessLogic.Domain;

/// <summary>Persistence boundary for the v1 application. Only domain types cross this boundary.</summary>
public interface IPlatformRepository
{
    Task<Page<Airport>> AirportsAsync(AirportFilter filter, Paging paging, CancellationToken ct = default);
    Task<Airport?> AirportAsync(int id, CancellationToken ct = default);
    Task<Page<Aircraft>> AircraftAsync(AircraftFilter filter, Paging paging, CancellationToken ct = default);
    Task<Aircraft?> AircraftAsync(int id, CancellationToken ct = default);
    Task<Page<Flight>> FlightsAsync(FlightQuery filter, Paging paging, CancellationToken ct = default);
    Task<Flight?> FlightAsync(int id, CancellationToken ct = default);
    Task<Page<Position>> PositionsAsync(int flightId, PositionQuery filter, Paging paging, CancellationToken ct = default);
    Task<IReadOnlyList<CurrentFlight>> CurrentFlightsAsync(MapQuery filter, CancellationToken ct = default);
    Task<CongestionResult> CongestionAsync(int? airportId, AnalysisPeriod period, Paging paging, CancellationToken ct = default);
    Task<IReadOnlyList<CongestionEntry>> ExportEntriesAsync(int? airportId, AnalysisPeriod period, CancellationToken ct = default);
    Task<Page<User>> UsersAsync(string? role, Paging paging, CancellationToken ct = default);
    Task<UserAccount?> UserAsync(int id, CancellationToken ct = default);
    Task<UserAccount?> UserByNameAsync(string username, CancellationToken ct = default);
    Task<User> CreateUserAsync(string username, string email, string passwordHash, string role, CancellationToken ct = default);
    Task<User?> UpdateUserAsync(int id, string username, string email, string? passwordHash, string role, CancellationToken ct = default);
    Task<bool> DeleteUserAsync(int id, CancellationToken ct = default);
    Task<Page<SavedReport>> ReportsAsync(int userId, Paging paging, CancellationToken ct = default);
    Task<SavedReport?> ReportAsync(int id, int userId, CancellationToken ct = default);
    Task<SavedReport> CreateReportAsync(int userId, string name, int airportId, AnalysisPeriod period, CancellationToken ct = default);
    Task<bool> DeleteReportAsync(int id, int userId, CancellationToken ct = default);
}
