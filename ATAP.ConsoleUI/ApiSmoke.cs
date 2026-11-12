using ATAP.ApiClient;
using ATAP.BusinessLogic.DTO;

namespace ATAP.ConsoleUI;

/// <summary>Repeatable check of the same HTTP adapter used by the interactive menus.</summary>
internal static class ApiSmoke
{
    public static async Task RunAsync(AtapApiClient client)
    {
        var airports = await client.GetAirportsAsync();
        var flights = await client.GetCurrentFlightsAsync();
        if (airports.Count == 0) throw new InvalidOperationException("Нет аэропортов");
        var user = await client.LoginAsync(
            Environment.GetEnvironmentVariable("ATAP_SMOKE_USER") ?? "admin",
            Environment.GetEnvironmentVariable("ATAP_SMOKE_PASSWORD") ?? "Admin#2026")
            ?? throw new InvalidOperationException("Вход через API не выполнен");
        int? savedId = null;
        try
        {
            var day = new DateOnly(2026, 6, 10);
            var report = await client.GetCongestionAsync(airports[0].Id,
                day.ToDateTime(TimeOnly.MinValue), day.ToDateTime(TimeOnly.MinValue));
            savedId = await client.SaveAsync(user.UserId, new CreateSavedReportDto
            { Name = "TUI HTTP smoke", AirportId = airports[0].Id, DateFrom = day, DateTo = day });
            if (!(await client.GetByUserAsync(user.UserId)).Any(r => r.Id == savedId))
                throw new InvalidOperationException("Сохранённый отчёт не найден");
            var csv = await client.ExportToCsvAsync(report);
            if (csv.Length == 0) throw new InvalidOperationException("CSV пуст");
            Console.WriteLine($"TUI HTTP OK: airports={airports.Count}, flights={flights.Count}, report={savedId}, CSV={csv.Length} bytes");
        }
        finally
        {
            try { if (savedId is { } id) await client.DeleteAsync(id, user.UserId); }
            finally { await client.LogoutAsync(); }
        }
    }
}
