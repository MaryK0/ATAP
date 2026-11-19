using Npgsql;

namespace ATAP.Api.Infrastructure;

public static class HealthEndpoints
{
    public static void MapAtapHealth(this WebApplication app, string connectionString, string? blazorUrl)
    {
        app.MapGet("/internal/health", () => Results.Text("alive\n"));
        app.MapGet("/internal/ready", async (HttpContext context) =>
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
            deadline.CancelAfter(TimeSpan.FromSeconds(3));
            context.Response.Headers.CacheControl = "no-store";
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(deadline.Token);
                await using var command = new NpgsqlCommand("SELECT 1", connection);
                await command.ExecuteScalarAsync(deadline.Token);
                if (!string.IsNullOrWhiteSpace(blazorUrl))
                {
                    using var client = new HttpClient();
                    using var response = await client.GetAsync(blazorUrl, deadline.Token);
                    response.EnsureSuccessStatusCode();
                }
                return Results.Text("ready\n");
            }
            catch (Exception exception) when (exception is NpgsqlException or HttpRequestException or OperationCanceledException)
            {
                return Results.Text("not ready\n", statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });
    }
}
