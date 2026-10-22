using Npgsql;
using ATAP.BusinessLogic.Interfaces.Repositories;
using ATAP.DataAccess.Interfaces;

namespace ATAP.DataAccess.Repositories;

public class PostgresUserRepository : IUserRepository
{
    private readonly string _connectionString;
    private readonly ISqlQueryFactory _sqlFactory;

    public PostgresUserRepository(string connectionString, ISqlQueryFactory sqlFactory)
    {
        _connectionString = connectionString;
        _sqlFactory = sqlFactory;
    }

    public async Task<UserDb?> GetByUsernameAsync(string username)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
       
        using var cmd = new NpgsqlCommand(_sqlFactory.GetUserByUsernameQuery(), connection);
        
        cmd.Parameters.AddWithValue("@username", username);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new UserDb
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                Role = reader.GetString(4),
                CreatedAt = reader.IsDBNull(5) ? default : reader.GetDateTime(5)
            };
        }
        
        return null;
    }

    public async Task<UserDb?> GetByEmailAsync(string email)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        const string sql = @"
            SELECT user_id, username, email, password_hash, role
            FROM dds.users 
            WHERE email = @email";
        
        using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@email", email);
        
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new UserDb
            {
                UserId = reader.GetInt32(0),
                Username = reader.GetString(1),
                Email = reader.GetString(2),
                PasswordHash = reader.GetString(3),
                Role = reader.GetString(4)
            };
        }
        
        return null;
    }

    public async Task<int> CreateAsync(UserDb user)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        using var cmd = new NpgsqlCommand(_sqlFactory.CreateUserQuery(), connection);
        
        cmd.Parameters.AddWithValue("@username", user.Username);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
        cmd.Parameters.AddWithValue("@role", user.Role);
        
        var result = await cmd.ExecuteScalarAsync();
        return result != null ? Convert.ToInt32(result) : 0;
    }

    public async Task<bool> UpdateAsync(UserDb user)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        const string sql = @"
            UPDATE dds.users
            SET username = @username, email = @email, role = @role, password_hash = @passwordHash
            WHERE user_id = @userId";

        using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@userId", user.UserId);
        cmd.Parameters.AddWithValue("@username", user.Username);
        cmd.Parameters.AddWithValue("@email", user.Email);
        cmd.Parameters.AddWithValue("@role", user.Role);
        cmd.Parameters.AddWithValue("@passwordHash", user.PasswordHash);
        
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    public async Task<bool> DeleteAsync(int userId)
    {
        using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        
        const string sql = "DELETE FROM dds.users WHERE user_id = @userId";
        
        using var cmd = new NpgsqlCommand(sql, connection);
        cmd.Parameters.AddWithValue("@userId", userId);
        
        var rows = await cmd.ExecuteNonQueryAsync();
        return rows > 0;
    }

    // ✅ Метод GetAllAsync() теперь ВНУТРИ класса
    public async Task<List<UserDb>> GetAllAsync()
{
    var result = new List<UserDb>();
    
    using var connection = new NpgsqlConnection(_connectionString);
    await connection.OpenAsync();
    
    const string sql = @"
        SELECT user_id, username, email, password_hash, role, created_at
        FROM dds.users
        ORDER BY user_id";
    
    using var cmd = new NpgsqlCommand(sql, connection);
    using var reader = await cmd.ExecuteReaderAsync();
    
    while (await reader.ReadAsync())
    {
        result.Add(new UserDb
        {
            UserId = reader.GetInt32(0),
            Username = reader.GetString(1),
            Email = reader.GetString(2),
            PasswordHash = reader.GetString(3),
            Role = reader.GetString(4),
            CreatedAt = reader.GetDateTime(5)
        });
    }
    
    return result;
}
}