using ATAP.BusinessLogic.Domain;
using ATAP.DataAccess.Models;
using Npgsql;

namespace ATAP.DataAccess.Repositories;

/// <summary>v1 persistence adapter. SQL and database models stay outside the application core.</summary>
public sealed class PostgresPlatformRepository(string connectionString) : IPlatformRepository
{
    private const string AirportColumns = "airport_id,icao_code,iata_code,name,city,country_code,latitude,longitude,analysis_radius,airport_type,elevation";
    private const string UserColumns = "user_id,username,email,password_hash,role,created_at";
    private const string AircraftColumns = "aircraft_id,icao24,registration,model,manufacturer,origin_country";
    private const string FlightColumns = "flight_id,callsign,aircraft_id,first_seen,last_seen";
    private const string PositionColumns = "position_id,flight_id,time_position,latitude,longitude,altitude,velocity,heading,on_ground";
    private const string CongestionColumns = "id,airport_id,flight_id,callsign,entry_time,exit_time,duration_seconds,is_departure,is_arrival,max_altitude_in_zone,min_altitude_in_zone";
    private const string ReportColumns = "r.id,r.user_id,r.name,r.airport_id,a.name AS airport_name,r.date_from,r.date_to,r.created_at";
    private const string ReportSource = "dds.saved_reports r JOIN dds.airports a ON a.airport_id=r.airport_id";

    public Task<Page<Airport>> AirportsAsync(AirportFilter filter, Paging paging, CancellationToken ct = default)
    {
        var f = new Filter();
        f.Add("icao_code ILIKE @icao", "icao", filter.IcaoCode);
        f.Add("airport_type=@type", "type", filter.Type);
        f.Add("country_code ILIKE @country", "country", filter.CountryCode);
        f.Add("(name ILIKE @q OR city ILIKE @q OR icao_code ILIKE @q OR iata_code ILIKE @q)", "q", filter.Q is null ? null : $"%{filter.Q}%");
        return PageAsync(AirportColumns, "dds.airports", f, "airport_id", paging, ReadAirport, ct);
    }
    public Task<Airport?> AirportAsync(int id, CancellationToken ct = default) =>
        OneAsync($"SELECT {AirportColumns} FROM dds.airports WHERE airport_id=@id", id, ReadAirport, ct);

    public Task<Page<Aircraft>> AircraftAsync(AircraftFilter filter, Paging paging, CancellationToken ct = default)
    {
        var f = new Filter();
        f.Add("icao24=@icao", "icao", filter.Icao24?.ToUpperInvariant());
        f.Add("origin_country ILIKE @country", "country", filter.OriginCountry);
        return PageAsync(AircraftColumns, "dds.aircrafts", f, "aircraft_id", paging, ReadAircraft, ct);
    }
    public Task<Aircraft?> AircraftAsync(int id, CancellationToken ct = default) =>
        OneAsync($"SELECT {AircraftColumns} FROM dds.aircrafts WHERE aircraft_id=@id", id, ReadAircraft, ct);

    public Task<Page<Flight>> FlightsAsync(FlightQuery filter, Paging paging, CancellationToken ct = default)
    {
        var f = new Filter();
        f.Add("callsign ILIKE @callsign", "callsign", filter.Callsign);
        f.Add("aircraft_id=@aircraft", "aircraft", filter.AircraftId);
        f.Add("first_seen>=@start", "start", filter.SeenFrom);
        f.Add("last_seen<=@end", "end", filter.SeenTo);
        return PageAsync(FlightColumns, "dds.flights", f, "flight_id DESC", paging, ReadFlight, ct);
    }
    public Task<Flight?> FlightAsync(int id, CancellationToken ct = default) =>
        OneAsync($"SELECT {FlightColumns} FROM dds.flights WHERE flight_id=@id", id, ReadFlight, ct);

    public Task<Page<Position>> PositionsAsync(int flightId, PositionQuery filter, Paging paging, CancellationToken ct = default)
    {
        var f = new Filter();
        f.Add("flight_id=@flight", "flight", flightId);
        f.Add("time_position>=@start", "start", filter.From);
        f.Add("time_position<=@end", "end", filter.To);
        return PageAsync(PositionColumns, "dds.positions", f, "time_position,position_id", paging, ReadPosition, ct);
    }
    public async Task<IReadOnlyList<CurrentFlight>> CurrentFlightsAsync(MapQuery filter, CancellationToken ct = default)
    {
        var f = new Filter();
        if (filter.MinLon is { } min && filter.MaxLon is { } max)
        {
            f.Add(min <= max ? "longitude BETWEEN @minLon AND @maxLon" : "(longitude>=@minLon OR longitude<=@maxLon)", "minLon", min);
            f.Parameters.Add(new("maxLon", max));
            f.Add("latitude>=@minLat", "minLat", filter.MinLat);
            f.Add("latitude<=@maxLat", "maxLat", filter.MaxLat);
        }
        f.Add("altitude>=@minAltitude", "minAltitude", filter.MinAltitude);
        f.Add("altitude<=@maxAltitude", "maxAltitude", filter.MaxAltitude);
        f.Add("origin_country ILIKE @country", "country", filter.OriginCountry);
        // Deduplicate legacy matview rows: map needs exactly one latest position per flight.
        return await QueryAsync("SELECT flight_id,icao24,callsign,origin_country,aircraft_model,latitude,longitude,altitude,velocity,true_track,on_ground " +
            "FROM (SELECT DISTINCT ON (flight_id) * FROM mart.current_flights_map ORDER BY flight_id,position_time DESC) m" +
            f.Where + " ORDER BY flight_id", f.Parameters, ReadMap, ct);
    }

    private static Filter CongestionFilter(int? airportId, AnalysisPeriod period)
    {
        var f = new Filter();
        f.Add("airport_id=@airport", "airport", airportId);
        f.Add("entry_time>=@start", "start", period.Start);
        f.Add("entry_time<@end", "end", period.EndExclusive);
        return f;
    }
    public async Task<CongestionResult> CongestionAsync(int? airportId, AnalysisPeriod period, Paging paging, CancellationToken ct = default)
    {
        var f = CongestionFilter(airportId, period);
        var page = await PageAsync(CongestionColumns, "mart.mart_airport_congestion", f, "entry_time DESC,id DESC", paging, ReadCongestion, ct);
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand("SELECT count(*) FILTER (WHERE is_departure),count(*) FILTER (WHERE is_arrival) FROM mart.mart_airport_congestion" + f.Where, connection);
        CopyParameters(command, f.Parameters);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new(airportId, period.From, period.To, page, checked((int)reader.GetInt64(0)), checked((int)reader.GetInt64(1)));
    }
    public async Task<IReadOnlyList<CongestionEntry>> ExportEntriesAsync(int? airportId, AnalysisPeriod period, CancellationToken ct = default)
    {
        var f = CongestionFilter(airportId, period);
        return await QueryAsync($"SELECT {CongestionColumns} FROM mart.mart_airport_congestion" + f.Where + " ORDER BY entry_time DESC,id DESC", f.Parameters, ReadCongestion, ct);
    }

    public Task<Page<User>> UsersAsync(string? role, Paging paging, CancellationToken ct = default)
    {
        var f = new Filter(); f.Add("role=@role", "role", role);
        return PageAsync(UserColumns, "dds.users", f, "user_id", paging, r => ReadUser(r).User, ct);
    }
    public Task<UserAccount?> UserAsync(int id, CancellationToken ct = default) =>
        OneAsync($"SELECT {UserColumns} FROM dds.users WHERE user_id=@id", id, ReadUser, ct);
    public async Task<UserAccount?> UserByNameAsync(string username, CancellationToken ct = default) =>
        (await QueryAsync($"SELECT {UserColumns} FROM dds.users WHERE username=@username", [new("username", username)], ReadUser, ct)).FirstOrDefault();
    public async Task<User> CreateUserAsync(string username, string email, string passwordHash, string role, CancellationToken ct = default)
    {
        var rows = await UserWriteAsync($"INSERT INTO dds.users(username,email,password_hash,role) VALUES(@username,@email,@hash,@role) RETURNING {UserColumns}",
            [new("username", username), new("email", email), new("hash", passwordHash), new("role", role)], ct);
        return rows.Single().User;
    }
    public async Task<User?> UpdateUserAsync(int id, string username, string email, string? passwordHash, string role, CancellationToken ct = default)
    {
        var parameters = new List<NpgsqlParameter> { new("username", username), new("email", email), new("role", role), new("id", id) };
        var hashSql = "";
        if (passwordHash is not null) { hashSql = ",password_hash=@hash"; parameters.Add(new("hash", passwordHash)); }
        var rows = await UserWriteAsync($"UPDATE dds.users SET username=@username,email=@email,role=@role{hashSql} WHERE user_id=@id RETURNING {UserColumns}", parameters, ct);
        return rows.FirstOrDefault()?.User;
    }
    private async Task<List<UserAccount>> UserWriteAsync(string sql, IEnumerable<NpgsqlParameter> parameters, CancellationToken ct)
    {
        try { return await QueryAsync(sql, parameters, ReadUser, ct); }
        catch (PostgresException e) when (e.SqlState == "23505")
        { throw new DomainException(DomainError.Conflict, "user-conflict", "Логин или email уже занят"); }
    }
    public Task<bool> DeleteUserAsync(int id, CancellationToken ct = default) =>
        DeleteAsync("DELETE FROM dds.users WHERE user_id=@id AND role<>'admin'", [new("id", id)], ct);

    public Task<Page<SavedReport>> ReportsAsync(int userId, Paging paging, CancellationToken ct = default)
    {
        var f = new Filter(); f.Add("r.user_id=@user", "user", userId);
        return PageAsync(ReportColumns, ReportSource, f, "r.created_at DESC,r.id DESC", paging, ReadReport, ct);
    }
    public async Task<SavedReport?> ReportAsync(int id, int userId, CancellationToken ct = default) =>
        (await QueryAsync($"SELECT {ReportColumns} FROM {ReportSource} WHERE r.id=@id AND r.user_id=@user",
            [new("id", id), new("user", userId)], ReadReport, ct)).FirstOrDefault();
    public async Task<SavedReport> CreateReportAsync(int userId, string name, int airportId, AnalysisPeriod period, CancellationToken ct = default)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand("INSERT INTO dds.saved_reports(user_id,name,airport_id,date_from,date_to) VALUES(@user,@name,@airport,@start,@end) RETURNING id", connection);
        command.Parameters.AddWithValue("user", userId); command.Parameters.AddWithValue("name", name);
        command.Parameters.AddWithValue("airport", airportId); command.Parameters.AddWithValue("start", period.From); command.Parameters.AddWithValue("end", period.To);
        var id = (int)(await command.ExecuteScalarAsync(ct))!;
        return (await ReportAsync(id, userId, ct))!;
    }
    public Task<bool> DeleteReportAsync(int id, int userId, CancellationToken ct = default) =>
        DeleteAsync("DELETE FROM dds.saved_reports WHERE id=@id AND user_id=@user", [new("id", id), new("user", userId)], ct);

    private async Task<Page<T>> PageAsync<T>(string columns, string source, Filter filter, string order, Paging paging, Func<NpgsqlDataReader, T> read, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand($"SELECT count(*) FROM {source}{filter.Where};SELECT {columns} FROM {source}{filter.Where} ORDER BY {order} LIMIT @limit OFFSET @offset", connection);
        CopyParameters(command, filter.Parameters);
        command.Parameters.AddWithValue("limit", paging.Limit); command.Parameters.AddWithValue("offset", paging.Offset);
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct); var total = checked((int)reader.GetInt64(0));
        await reader.NextResultAsync(ct); var rows = new List<T>();
        while (await reader.ReadAsync(ct)) rows.Add(read(reader));
        return new(rows, total, paging.Limit, paging.Offset);
    }
    private async Task<T?> OneAsync<T>(string sql, int id, Func<NpgsqlDataReader, T> read, CancellationToken ct) where T : class =>
        (await QueryAsync(sql, [new("id", id)], read, ct)).FirstOrDefault();
    private async Task<List<T>> QueryAsync<T>(string sql, IEnumerable<NpgsqlParameter> parameters, Func<NpgsqlDataReader, T> read, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct);
        await using var command = new NpgsqlCommand(sql, connection); CopyParameters(command, parameters);
        await using var reader = await command.ExecuteReaderAsync(ct); var rows = new List<T>();
        while (await reader.ReadAsync(ct)) rows.Add(read(reader)); return rows;
    }
    private async Task<bool> DeleteAsync(string sql, IEnumerable<NpgsqlParameter> parameters, CancellationToken ct)
    {
        await using var connection = await OpenAsync(ct); await using var command = new NpgsqlCommand(sql, connection);
        CopyParameters(command, parameters); return await command.ExecuteNonQueryAsync(ct) > 0;
    }
    private async Task<NpgsqlConnection> OpenAsync(CancellationToken ct)
    {
        var connection = new NpgsqlConnection(connectionString);
        try { await connection.OpenAsync(ct); return connection; }
        catch { await connection.DisposeAsync(); throw; }
    }
    private static void CopyParameters(NpgsqlCommand command, IEnumerable<NpgsqlParameter> parameters)
    { foreach (var p in parameters) command.Parameters.Add(new NpgsqlParameter(p.ParameterName, p.Value)); }
    private sealed class Filter
    {
        private readonly List<string> _clauses = [];
        public List<NpgsqlParameter> Parameters { get; } = [];
        public string Where => _clauses.Count == 0 ? "" : " WHERE " + string.Join(" AND ", _clauses);
        public void Add(string clause, string name, object? value)
        { if (value is null) return; _clauses.Add(clause); Parameters.Add(new(name, value)); }
    }
    private static string? Text(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i).TrimEnd();
    private static T? Nullable<T>(NpgsqlDataReader r, int i) where T : struct => r.IsDBNull(i) ? null : r.GetFieldValue<T>(i);
    private static double Number(NpgsqlDataReader r, int i) => Convert.ToDouble(r.GetValue(i), System.Globalization.CultureInfo.InvariantCulture);
    private static double? OptionalNumber(NpgsqlDataReader r, int i) => r.IsDBNull(i) ? null : Number(r, i);
    private static UserAccount ReadUser(NpgsqlDataReader r) => new UserRow(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetString(3), r.GetString(4), r.GetDateTime(5)).ToDomain();
    private static Aircraft ReadAircraft(NpgsqlDataReader r) => new AircraftRow(r.GetInt32(0), r.GetString(1), Text(r, 2), Text(r, 3), Text(r, 4), Text(r, 5)).ToDomain();
    private static Flight ReadFlight(NpgsqlDataReader r) => new FlightRow(r.GetInt32(0), r.GetString(1), Nullable<int>(r, 2), Nullable<DateTime>(r, 3), Nullable<DateTime>(r, 4)).ToDomain();
    private static Airport ReadAirport(NpgsqlDataReader r) => new AirportRow(r.GetInt32(0), r.GetString(1).Trim(), Text(r, 2), r.GetString(3), Text(r, 4), Text(r, 5), Number(r, 6), Number(r, 7), r.GetInt32(8), Text(r, 9), Nullable<int>(r, 10)).ToDomain();
    private static Position ReadPosition(NpgsqlDataReader r) => new PositionRow(r.GetInt64(0), r.GetInt32(1), r.GetDateTime(2), Number(r, 3), Number(r, 4), Nullable<int>(r, 5), OptionalNumber(r, 6), r.IsDBNull(7) ? null : Convert.ToInt32(r.GetValue(7)), Nullable<bool>(r, 8)).ToDomain();
    private static CurrentFlight ReadMap(NpgsqlDataReader r) => new MapRow(r.GetInt32(0), r.GetString(1), r.GetString(2), Text(r, 3), Text(r, 4), Number(r, 5), Number(r, 6), Nullable<int>(r, 7), OptionalNumber(r, 8), r.IsDBNull(9) ? null : Convert.ToInt32(r.GetValue(9)), Nullable<bool>(r, 10)).ToDomain();
    private static CongestionEntry ReadCongestion(NpgsqlDataReader r) => new CongestionRow(r.GetInt64(0), r.GetInt32(1), r.GetInt32(2), r.GetString(3), r.GetDateTime(4), r.GetDateTime(5), r.GetInt32(6), r.GetBoolean(7), r.GetBoolean(8), Nullable<int>(r, 9), Nullable<int>(r, 10)).ToDomain();
    private static SavedReport ReadReport(NpgsqlDataReader r) => new ReportRow(r.GetInt32(0), r.GetInt32(1), r.GetString(2), r.GetInt32(3), r.GetString(4), r.GetFieldValue<DateOnly>(5), r.GetFieldValue<DateOnly>(6), r.GetDateTime(7)).ToDomain();
}
