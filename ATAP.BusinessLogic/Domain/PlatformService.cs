using System.Globalization;
using System.Net.Mail;
using System.Text;
using ATAP.BusinessLogic.Security;

namespace ATAP.BusinessLogic.Domain;

public sealed class PlatformService(IPlatformRepository repository,
    ATAP.BusinessLogic.Interfaces.Repositories.ICacheRepository? cache = null)
{
    public Task<Page<Airport>> AirportsAsync(AirportFilter filter, Paging paging, CancellationToken ct = default)
    { paging.Validate(10000); return repository.AirportsAsync(filter, paging, ct); }
    public async Task<Airport> AirportAsync(int id, CancellationToken ct = default) =>
        await repository.AirportAsync(id, ct) ?? throw Missing("Аэропорт");
    public Task<Page<Aircraft>> AircraftAsync(AircraftFilter filter, Paging paging, CancellationToken ct = default)
    { paging.Validate(); return repository.AircraftAsync(filter, paging, ct); }
    public async Task<Aircraft> AircraftAsync(int id, CancellationToken ct = default) =>
        await repository.AircraftAsync(id, ct) ?? throw Missing("Воздушное судно");
    public Task<Page<Flight>> FlightsAsync(FlightQuery filter, Paging paging, CancellationToken ct = default)
    {
        paging.Validate();
        if (filter.SeenFrom > filter.SeenTo) throw Invalid("Дата начала позже даты окончания");
        return repository.FlightsAsync(filter, paging, ct);
    }
    public async Task<Flight> FlightAsync(int id, CancellationToken ct = default) =>
        await repository.FlightAsync(id, ct) ?? throw Missing("Рейс");
    public async Task<Page<Position>> PositionsAsync(int id, PositionQuery filter, Paging paging, CancellationToken ct = default)
    {
        paging.Validate(); if (filter.From > filter.To) throw Invalid("Дата начала позже даты окончания");
        await FlightAsync(id, ct); return await repository.PositionsAsync(id, filter, paging, ct);
    }
    public Task<IReadOnlyList<CurrentFlight>> CurrentFlightsAsync(MapQuery filter, CancellationToken ct = default) => repository.CurrentFlightsAsync(filter, ct);
    public async Task ValidateAnalysisAsync(int? airportId, AnalysisPeriod period, CancellationToken ct = default)
    {
        period.Validate();
        if (airportId is < 1) throw Invalid("Идентификатор аэропорта должен быть положительным");
        if (airportId is { } id && await repository.AirportAsync(id, ct) is null)
            throw new DomainException(DomainError.Invalid, "unknown-airport", "Аэропорт не найден");
    }
    public async Task<CongestionResult> CongestionAsync(int? airportId, AnalysisPeriod period, Paging paging, CancellationToken ct = default)
    {
        paging.Validate(); await ValidateAnalysisAsync(airportId, period, ct);
        var key = $"v1:congestion:{airportId}:{period.From:yyyyMMdd}:{period.To:yyyyMMdd}:{paging.Limit}:{paging.Offset}";
        if (cache is not null && await cache.GetAsync<CongestionResult>(key) is { } hit) return hit;
        var result = await repository.CongestionAsync(airportId, period, paging, ct);
        if (cache is not null) await cache.SetAsync(key, result, 3600);
        return result;
    }
    public async Task<byte[]> ExportCsvAsync(int? airportId, AnalysisPeriod period, CancellationToken ct = default)
    { await ValidateAnalysisAsync(airportId, period, ct); return Csv(await repository.ExportEntriesAsync(airportId, period, ct)); }

    public async Task<User?> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        var account = await repository.UserByNameAsync(username, ct);
        if (account is null || !PasswordHasher.Verify(password, account.PasswordHash)) return null;
        if (PasswordHasher.NeedsUpgrade(account.PasswordHash))
            await repository.UpdateUserAsync(account.User.Id, account.User.Username, account.User.Email, PasswordHasher.Hash(password), account.User.Role, ct);
        return account.User;
    }
    public async Task<User> UserAsync(int id, CancellationToken ct = default) =>
        (await repository.UserAsync(id, ct))?.User ?? throw Missing("Пользователь");
    public Task<Page<User>> UsersAsync(string? role, Paging paging, CancellationToken ct = default)
    { paging.Validate(); if (role is not null) ValidateRole(role); return repository.UsersAsync(role, paging, ct); }
    public Task<User> CreateUserAsync(string username, string email, string password, string role, CancellationToken ct = default)
    { ValidateProfile(username, email, role); ValidatePassword(password); return repository.CreateUserAsync(username, email, PasswordHasher.Hash(password), role, ct); }
    public async Task<User> UpdateUserAsync(int id, string? username, string? email, string? role, string? password, CancellationToken ct = default)
    {
        var current = await UserAsync(id, ct); username ??= current.Username; email ??= current.Email; role ??= current.Role;
        ValidateProfile(username, email, role); if (password is not null) ValidatePassword(password);
        return await repository.UpdateUserAsync(id, username, email, password is null ? null : PasswordHasher.Hash(password), role, ct) ?? throw Missing("Пользователь");
    }
    public async Task DeleteUserAsync(int id, CancellationToken ct = default)
    {
        if ((await UserAsync(id, ct)).Role == "admin") throw new DomainException(DomainError.Forbidden, "cannot-delete-admin", "Нельзя удалить пользователя с ролью admin");
        if (!await repository.DeleteUserAsync(id, ct)) throw Missing("Пользователь");
    }
    public Task<Page<SavedReport>> ReportsAsync(int userId, Paging paging, CancellationToken ct = default)
    { paging.Validate(); return repository.ReportsAsync(userId, paging, ct); }
    public async Task<SavedReport> ReportAsync(int id, int userId, CancellationToken ct = default) =>
        await repository.ReportAsync(id, userId, ct) ?? throw Missing("Отчёт");
    public async Task<SavedReport> CreateReportAsync(int userId, string name, int airportId, AnalysisPeriod period, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 100) throw Invalid("Название отчёта должно содержать от 1 до 100 символов");
        await ValidateAnalysisAsync(airportId, period, ct); return await repository.CreateReportAsync(userId, name.Trim(), airportId, period, ct);
    }
    public async Task DeleteReportAsync(int id, int userId, CancellationToken ct = default)
    { if (!await repository.DeleteReportAsync(id, userId, ct)) throw Missing("Отчёт"); }
    private static void ValidateProfile(string username, string email, string role)
    {
        if (string.IsNullOrWhiteSpace(username) || username.Length > 50) throw Invalid("Логин должен содержать от 1 до 50 символов");
        if (email.Length > 100 || !MailAddress.TryCreate(email, out var address) || address.Address != email) throw Invalid("Укажите корректный email");
        ValidateRole(role);
    }
    private static void ValidateRole(string role)
    { if (role is not ("analyst" or "admin")) throw Invalid("Допустимы роли analyst и admin"); }
    private static void ValidatePassword(string password)
    { if (password.Length is < 8 or > 128) throw Invalid("Пароль должен содержать от 8 до 128 символов"); }
    private static DomainException Invalid(string text) => new(DomainError.Invalid, "validation-error", text);
    private static DomainException Missing(string text) => new(DomainError.NotFound, "not-found", text + " не найден");

    public static byte[] Csv(IEnumerable<CongestionEntry> entries)
    {
        var csv = new StringBuilder("id,airportId,flightId,callsign,entryTime,exitTime,durationSeconds,isDeparture,isArrival,maxAltitudeInZone,minAltitudeInZone\n");
        foreach (var e in entries)
        {
            string Escape(string? value) => value is null ? "" : value.IndexOfAny([',', '"', '\n', '\r']) < 0 ? value : "\"" + value.Replace("\"", "\"\"") + "\"";
            csv.AppendJoin(',', new[]{e.Id.ToString(CultureInfo.InvariantCulture),e.AirportId.ToString(CultureInfo.InvariantCulture),e.FlightId.ToString(CultureInfo.InvariantCulture),Escape(e.Callsign),
                e.EntryTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture),e.ExitTime.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ",CultureInfo.InvariantCulture),
                e.DurationSeconds.ToString(CultureInfo.InvariantCulture),e.IsDeparture?"true":"false",e.IsArrival?"true":"false",e.MaxAltitudeInZone?.ToString(CultureInfo.InvariantCulture)??"",e.MinAltitudeInZone?.ToString(CultureInfo.InvariantCulture)??""}).Append('\n');
        }
        return Encoding.UTF8.GetBytes(csv.ToString());
    }
}
