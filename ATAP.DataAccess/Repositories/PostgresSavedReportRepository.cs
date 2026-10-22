using Npgsql;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess.Interfaces;

namespace ATAP.DataAccess.Repositories;

public class PostgresSavedReportRepository : ISavedReportRepository
{
    private readonly string _connectionString;
    private readonly ISqlQueryFactory _sqlFactory;

    public PostgresSavedReportRepository(string connectionString, ISqlQueryFactory sqlFactory)
    {
        _connectionString = connectionString;
        _sqlFactory = sqlFactory;
    }

    public async Task<List<SavedReportDto>> GetByUserAsync(int userId)
    {
        var result = new List<SavedReportDto>();
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new NpgsqlCommand(SqlQueries.GetSavedReportsByUser, connection);
        cmd.Parameters.AddWithValue("@userId", userId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new SavedReportDto
            {
                Id          = reader.GetInt32(0),
                Name        = reader.GetString(1),
                AirportId   = reader.GetInt32(2),
                AirportName = reader.GetString(3),
                DateFrom    = DateOnly.FromDateTime(reader.GetDateTime(4)),
                DateTo      = DateOnly.FromDateTime(reader.GetDateTime(5)),
                CreatedAt   = reader.GetDateTime(6)
            });
        }
        return result;
    }

    public async Task<int> CreateAsync(int userId, CreateSavedReportDto dto)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new NpgsqlCommand(SqlQueries.CreateSavedReport, connection);
        cmd.Parameters.AddWithValue("@userId",    userId);
        cmd.Parameters.AddWithValue("@name",      dto.Name);
        cmd.Parameters.AddWithValue("@airportId", dto.AirportId);
        cmd.Parameters.AddWithValue("@dateFrom",  dto.DateFrom.ToDateTime(TimeOnly.MinValue));
        cmd.Parameters.AddWithValue("@dateTo",    dto.DateTo.ToDateTime(TimeOnly.MinValue));

        return (int)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task DeleteAsync(int id, int userId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var cmd = new NpgsqlCommand(SqlQueries.DeleteSavedReport, connection);
        cmd.Parameters.AddWithValue("@id",     id);
        cmd.Parameters.AddWithValue("@userId", userId);

        await cmd.ExecuteNonQueryAsync();
    }
}
