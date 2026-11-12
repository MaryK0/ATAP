using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;

namespace ATAP.ApiClient;

/// <summary>HTTP adapter for the existing UI service interfaces. No database dependencies.</summary>
public sealed class AtapApiClient(HttpClient http) : IAuthService, IMapService, IAnalyticsService, IAdminService, ISavedReportService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public string? AccessToken { get; private set; }
    private UserDto? _user;
    public async Task<UserDto?> LoginAsync(string username, string password)
    {
        try
        {
            var login = await SendAsync<LoginResponse>(HttpMethod.Post, "login", new { username, password });
            AccessToken = login.AccessToken; return _user = User(login.User);
        }
        catch (ApiClientException e) when (e.StatusCode == 401) { return null; }
    }
    public async Task<bool> RegisterAsync(string username, string email, string password)
    {
        try { await SendAsync<UserData>(HttpMethod.Post, "users", new { username, email, password }); return true; }
        catch (ApiClientException e) when (e.StatusCode == 409) { return false; }
    }
    // Existing UI service interface: the login response supplies the local user state.
    // Token validity and the current role are checked by the server on protected requests.
    public Task<UserDto?> GetCurrentUserAsync() => Task.FromResult(_user);
    public async Task LogoutAsync()
    {
        try { if (AccessToken is not null) await SendAsync<JsonElement>(HttpMethod.Post, "logout"); }
        finally { AccessToken = null; _user = null; }
    }
    public Task<List<FlightDto>> GetCurrentFlightsAsync() => GetCurrentFlightsAsync(new FlightFilter());
    public async Task<List<FlightDto>> GetCurrentFlightsAsync(FlightFilter filter)
    {
        var query = new List<string>();
        if (filter.Area is { } b) query.Add("bbox=" + string.Join(',', new[] { b.MinLon, b.MinLat, b.MaxLon, b.MaxLat }.Select(n => n.ToString(CultureInfo.InvariantCulture))));
        if (filter.MinAltitude is { } min) query.Add("minAltitude=" + min);
        if (filter.MaxAltitude is { } max) query.Add("maxAltitude=" + max);
        if (filter.Country is { } country) query.Add("originCountry=" + Uri.EscapeDataString(country));
        var rows = await SendAsync<List<MapData>>(HttpMethod.Get, "flights/current?" + string.Join('&', query));
        return rows.Select(f => new FlightDto { Id = f.FlightId, Callsign = f.Callsign, Latitude = f.Latitude, Longitude = f.Longitude, Altitude = f.Altitude ?? 0, Country = f.OriginCountry }).ToList();
    }
    public async Task<List<AirportDto>> GetAirportsAsync() => await PagesAsync<AirportDto>("airports", 10000);
    public Task<CongestionReportDto> GetCongestionAsync(int airportId, DateTime start, DateTime end) => Analysis(airportId, start, end);
    public Task<CongestionReportDto> GetAllCongestionAsync(DateTime start, DateTime end) => Analysis(null, start, end);
    private async Task<CongestionReportDto> Analysis(int? airportId, DateTime start, DateTime end)
    {
        var url = $"congestion?dateFrom={start:yyyy-MM-dd}&dateTo={end:yyyy-MM-dd}" + (airportId is null ? "" : "&airportId=" + airportId);
        var first = await SendAsync<AnalysisData>(HttpMethod.Get, url + "&limit=1000&offset=0");
        var entries = first.Items.ToList();
        while (entries.Count < first.Total)
        {
            var page = await SendAsync<AnalysisData>(HttpMethod.Get, url + "&limit=1000&offset=" + entries.Count);
            if (page.Items.Count == 0) break; entries.AddRange(page.Items);
        }
        return new() { AirportId = airportId ?? 0, PeriodStart = start, PeriodEnd = end, DepartureCount = first.Summary.Departures, ArrivalCount = first.Summary.Arrivals, Entries = entries };
    }
    public async Task<byte[]> ExportToCsvAsync(CongestionReportDto report)
    {
        // The TUI demonstrates the asynchronous export lifecycle instead of generating CSV locally.
        var job = await SendAsync<ExportData>(HttpMethod.Post, "exports", new { airportId = report.AirportId == 0 ? (int?)null : report.AirportId, dateFrom = DateOnly.FromDateTime(report.PeriodStart), dateTo = DateOnly.FromDateTime(report.PeriodEnd) });
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (job.Status is "queued" or "running")
        {
            await Task.Delay(TimeSpan.FromSeconds(1), timeout.Token);
            job = await SendAsync<ExportData>(HttpMethod.Get, $"exports/{job.Id}", ct: timeout.Token);
        }
        if (job.Status != "completed") throw new InvalidOperationException(job.Error ?? "Выгрузка завершилась ошибкой");
        using var request = Request(HttpMethod.Get, $"exports/{job.Id}/file"); request.Headers.Accept.Add(new("text/csv"));
        using var response = await http.SendAsync(request, timeout.Token); await CheckAsync(response, timeout.Token);
        return await response.Content.ReadAsByteArrayAsync(timeout.Token);
    }
    public async Task<List<UserDto>> GetAllUsersAsync() => (await PagesAsync<UserData>("users")).Select(User).ToList();
    public async Task<bool> UpdateUserAsync(int userId, string username, string email, string? newPassword, string role)
    {
        var body = new Dictionary<string, object> { { "username", username }, { "email", email }, { "role", role } };
        if (!string.IsNullOrWhiteSpace(newPassword)) body["password"] = newPassword;
        try { await SendAsync<UserData>(HttpMethod.Patch, $"users/{userId}", body, "application/merge-patch+json"); return true; }
        catch (ApiClientException e) when (e.StatusCode == 404) { return false; }
    }
    public async Task<bool> DeleteUserAsync(int userId)
    {
        try { await SendAsync<JsonElement>(HttpMethod.Delete, $"users/{userId}"); return true; }
        catch (ApiClientException e) when (e.StatusCode == 404) { return false; }
    }
    public async Task<List<SavedReportDto>> GetByUserAsync(int userId) => await PagesAsync<SavedReportDto>("reports");
    public async Task<int> SaveAsync(int userId, CreateSavedReportDto dto) =>
        (await SendAsync<SavedReportDto>(HttpMethod.Post, "reports", new { name = dto.Name, airportId = dto.AirportId, dateFrom = dto.DateFrom, dateTo = dto.DateTo })).Id;
    public async Task DeleteAsync(int id, int userId) => await SendAsync<JsonElement>(HttpMethod.Delete, $"reports/{id}");

    private async Task<List<T>> PagesAsync<T>(string path, int limit = 1000)
    {
        var items = new List<T>();
        while (true)
        {
            var page = await SendAsync<PageData<T>>(HttpMethod.Get, $"{path}?limit={limit}&offset={items.Count}");
            items.AddRange(page.Items); if (page.Items.Count == 0 || items.Count >= page.Total) return items;
        }
    }
    private HttpRequestMessage Request(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        if (AccessToken is not null) request.Headers.Authorization = new("Bearer", AccessToken);
        return request;
    }
    private async Task<T> SendAsync<T>(HttpMethod method, string path, object? body = null, string type = "application/json", CancellationToken ct = default)
    {
        using var request = Request(method, path);
        if (body is not null) request.Content = JsonContent.Create(body, options: Json, mediaType: new MediaTypeHeaderValue(type));
        using var response = await http.SendAsync(request, ct); await CheckAsync(response, ct);
        if (response.StatusCode == HttpStatusCode.NoContent) return default!;
        return (await response.Content.ReadFromJsonAsync<T>(Json, ct))!;
    }
    private static async Task CheckAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode) return;
        var text = await response.Content.ReadAsStringAsync(ct); var message = $"API: {(int)response.StatusCode}";
        try { using var json = JsonDocument.Parse(text); if (json.RootElement.TryGetProperty("title", out var title)) message = title.GetString() ?? message; }
        catch (JsonException) { }
        throw new ApiClientException((int)response.StatusCode, message);
    }
    private static UserDto User(UserData u) => new() { UserId = u.Id, Username = u.Username, Email = u.Email, Role = u.Role, CreatedAt = u.CreatedAt };
    private sealed record UserData(int Id, string Username, string Email, string Role, DateTime CreatedAt);
    private sealed record LoginResponse(string AccessToken, string TokenType, int ExpiresIn, UserData User);
    private sealed record PageData<T>(List<T> Items, int Total, int Limit, int Offset);
    private sealed record MapData(int FlightId, string Callsign, double Latitude, double Longitude, int? Altitude, string? OriginCountry);
    private sealed record SummaryData(int Departures, int Arrivals);
    private sealed record AnalysisData(List<CongestionEntryDto> Items, int Total, SummaryData Summary);
    private sealed record ExportData(Guid Id, string Status, string? Error);
}
public sealed class ApiClientException(int statusCode, string message) : Exception(message)
{ public int StatusCode { get; } = statusCode; }
