using ATAP.BusinessLogic.Domain;
using Npgsql;
using NpgsqlTypes;

namespace ATAP.DataAccess.Repositories;

public sealed class PostgresExportRepository(string connectionString) : IExportRepository
{
    private sealed record ExportRow(Guid Id, int UserId, int? AirportId, DateOnly DateFrom, DateOnly DateTo, string Status, DateTime CreatedAt, byte[]? Result, string? Error)
    { public ReportExport ToDomain() => new(Id, UserId, AirportId, DateFrom, DateTo, Status, CreatedAt, Result, Error); }
    private const string Columns = "id,user_id,airport_id,date_from,date_to,status,created_at,result,error";
    public async Task<ReportExport> CreateAsync(int userId, int? airportId, AnalysisPeriod period, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"INSERT INTO dds.report_exports(id,user_id,airport_id,date_from,date_to) VALUES(@id,@user,@airport,@start,@end) RETURNING {Columns}", connection);
        command.Parameters.AddWithValue("id", Guid.NewGuid()); command.Parameters.AddWithValue("user", userId);
        command.Parameters.AddWithValue("airport", NpgsqlDbType.Integer, (object?)airportId ?? DBNull.Value);
        command.Parameters.AddWithValue("start", period.From); command.Parameters.AddWithValue("end", period.To);
        await using var reader = await command.ExecuteReaderAsync(ct); await reader.ReadAsync(ct); return Read(reader);
    }
    public async Task<ReportExport?> GetAsync(Guid id, int userId, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand($"SELECT {Columns} FROM dds.report_exports WHERE id=@id AND user_id=@user", connection);
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("user", userId);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? Read(reader) : null;
    }
    public async Task<ReportExport?> ClaimAsync(CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        // One atomic statement claims a job. Expired leases recover interrupted jobs after restart.
        await using var command = new NpgsqlCommand($"""
            UPDATE dds.report_exports SET status='running',lease_until=NOW()+INTERVAL '5 minutes'
            WHERE id=(SELECT id FROM dds.report_exports
                WHERE status='queued' OR (status='running' AND lease_until<NOW())
                ORDER BY created_at FOR UPDATE SKIP LOCKED LIMIT 1)
            RETURNING {Columns}
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(ct); return await reader.ReadAsync(ct) ? Read(reader) : null;
    }
    public async Task CompleteAsync(Guid id, byte[]? result, string? error, CancellationToken ct)
    {
        await using var connection = new NpgsqlConnection(connectionString); await connection.OpenAsync(ct);
        await using var command = new NpgsqlCommand("UPDATE dds.report_exports SET status=@status,result=@result,error=@error,lease_until=NULL WHERE id=@id", connection);
        command.Parameters.AddWithValue("id", id); command.Parameters.AddWithValue("status", error is null ? "completed" : "failed");
        command.Parameters.AddWithValue("result", NpgsqlDbType.Bytea, (object?)result ?? DBNull.Value);
        command.Parameters.AddWithValue("error", NpgsqlDbType.Text, (object?)error ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
    private static ReportExport Read(NpgsqlDataReader r) => new ExportRow(r.GetGuid(0), r.GetInt32(1), r.IsDBNull(2) ? null : r.GetInt32(2), r.GetFieldValue<DateOnly>(3), r.GetFieldValue<DateOnly>(4), r.GetString(5), r.GetDateTime(6), r.IsDBNull(7) ? null : r.GetFieldValue<byte[]>(7), r.IsDBNull(8) ? null : r.GetString(8)).ToDomain();
}
