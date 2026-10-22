using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ATAP.E2ETests;

/// <summary>
/// Демонстрационный сценарий MVP целиком, по HTTP, на развёрнутом стенде (PostgreSQL + Redis + ATAP.Api):
/// гость смотрит карту → пытается открыть аналитику и получает отказ → неверный и верный вход →
/// аналитик строит витрину загруженности (JSON и CSV), ошибки ввода → сохраняет отчёт, находит его в
/// списке и удаляет → выходит, и токен перестаёт действовать. GUI в тесте не участвует.
/// </summary>
[Trait("Category", "E2E")]
public class DemoScenario_AnalystAnalyzesCongestionAndSavesReport_E2ETest
{
    private const string Period = "airportId=1&dateFrom=2026-06-10&dateTo=2026-06-10";

    [Fact]
    public async Task DemoScenario_FromGuestMapToSavedReport_PassesEndToEnd()
    {
        // Arrange
        using var http = new HttpClient { BaseAddress = new Uri(E2EEnvironment.ApiBaseUrl), Timeout = TimeSpan.FromSeconds(30) };
        string? token = null;
        int? reportId = null;

        try
        {
            // ── Шаг 1. Гость открывает карту: аэропорты и маркеры судов (FR-04, FR-05) ──────────────
            var airports = await GetJson(http, "/api/v1/airports?limit=100");
            Assert.Equal(3, airports.GetProperty("total").GetInt32());
            Assert.Contains("UUEE", airports.GetProperty("items").EnumerateArray().Select(a => a.GetProperty("icaoCode").GetString()));

            using var flightsResponse = await http.GetAsync("/api/v1/flights/current");
            Assert.Equal(HttpStatusCode.OK, flightsResponse.StatusCode);
            var flights = JsonDocument.Parse(await flightsResponse.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(3, flights.GetArrayLength());   // счётчик «Активных рейсов» = числу маркеров
            var etag = flightsResponse.Headers.ETag?.Tag;
            Assert.False(string.IsNullOrEmpty(etag));

            using (var notModified = new HttpRequestMessage(HttpMethod.Get, "/api/v1/flights/current"))
            {
                notModified.Headers.IfNoneMatch.Add(new EntityTagHeaderValue(etag!));
                using var cached = await http.SendAsync(notModified);
                Assert.Equal(HttpStatusCode.NotModified, cached.StatusCode);   // снимок не изменился
            }

            var moscow = await GetJson(http, "/api/v1/flights/current?bbox=37,55,38,56");
            Assert.Equal(1, moscow.GetArrayLength());                          // видимая область: только рейс над Москвой
            Assert.Equal("AFL123", moscow[0].GetProperty("callsign").GetString());

            using var badBbox = await http.GetAsync("/api/v1/flights/current?bbox=1,2,3");
            await AssertProblem(badBbox, HttpStatusCode.BadRequest, "validation-error");

            // ── Шаг 2. Гость не получает аналитику ────────────────────────────────────────────────
            using var guestCongestion = await http.GetAsync($"/api/v1/congestion?{Period}");
            await AssertProblem(guestCongestion, HttpStatusCode.Unauthorized, "unauthorized");
            Assert.Contains("Bearer", guestCongestion.Headers.WwwAuthenticate.ToString());

            // ── Шаг 3. Неверный пароль: одинаковый отказ для неверного пароля и неизвестного логина ─
            using var wrongPassword = await PostJson(http, "/api/v1/sessions",
                new { username = E2EEnvironment.AnalystUsername, password = "wrong-password" });
            await AssertProblem(wrongPassword, HttpStatusCode.Unauthorized, "invalid-credentials");
            using var unknownUser = await PostJson(http, "/api/v1/sessions", new { username = "nobody", password = "wrong-password" });
            await AssertProblem(unknownUser, HttpStatusCode.Unauthorized, "invalid-credentials");

            // ── Шаг 4. Вход аналитика (FR-02) ─────────────────────────────────────────────────────
            using var login = await PostJson(http, "/api/v1/sessions",
                new { username = E2EEnvironment.AnalystUsername, password = E2EEnvironment.AnalystPassword });
            Assert.Equal(HttpStatusCode.Created, login.StatusCode);
            var session = JsonDocument.Parse(await login.Content.ReadAsStringAsync()).RootElement;
            token = session.GetProperty("accessToken").GetString();
            Assert.False(string.IsNullOrEmpty(token));
            Assert.Equal("Bearer", session.GetProperty("tokenType").GetString());
            Assert.Equal("analyst", session.GetProperty("user").GetProperty("role").GetString());
            Assert.False(session.GetProperty("user").TryGetProperty("password", out _));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            // ── Шаг 5. Аналитика: витрина за сутки (FR-07, FR-08, FR-09) ──────────────────────────
            var congestion = await GetJson(http, $"/api/v1/congestion?{Period}");
            var summary = congestion.GetProperty("summary");
            var total = congestion.GetProperty("total").GetInt32();
            Assert.Equal(4, total);
            Assert.Equal(1, summary.GetProperty("departures").GetInt32());
            Assert.Equal(3, summary.GetProperty("arrivals").GetInt32());
            Assert.Equal(total, summary.GetProperty("departures").GetInt32() + summary.GetProperty("arrivals").GetInt32());
            Assert.Equal(total, congestion.GetProperty("items").GetArrayLength());
            // обе границы периода включаются: запись 23:59:59 входит в сутки 10 июня
            Assert.Contains(congestion.GetProperty("items").EnumerateArray(),
                e => e.GetProperty("entryTime").GetString() == "2026-06-10T23:59:59Z");

            // ── Шаг 6. Выгрузка CSV: строк столько же, сколько записей (FR-10) ───────────────────
            using var csvRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/congestion?{Period}");
            csvRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/csv"));
            using var csv = await http.SendAsync(csvRequest);
            Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
            Assert.Equal("text/csv", csv.Content.Headers.ContentType?.MediaType);
            var lines = (await csv.Content.ReadAsStringAsync()).Split('\n', StringSplitOptions.RemoveEmptyEntries);
            Assert.StartsWith("id,airportId,flightId,callsign", lines[0]);
            Assert.Equal(total, lines.Length - 1);

            // ── Шаг 7. Ошибки ввода: период, аэропорт, формат ─────────────────────────────────────
            using var invertedPeriod = await http.GetAsync("/api/v1/congestion?airportId=1&dateFrom=2026-06-11&dateTo=2026-06-10");
            await AssertProblem(invertedPeriod, HttpStatusCode.BadRequest, "invalid-period");
            using var longPeriod = await http.GetAsync("/api/v1/congestion?airportId=1&dateFrom=2026-04-01&dateTo=2026-06-10");
            await AssertProblem(longPeriod, HttpStatusCode.BadRequest, "period-too-long");
            using var unknownAirport = await http.GetAsync("/api/v1/congestion?airportId=999&dateFrom=2026-06-10&dateTo=2026-06-10");
            await AssertProblem(unknownAirport, HttpStatusCode.BadRequest, "unknown-airport");
            using var xmlRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/v1/congestion?{Period}");
            xmlRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/xml"));
            using var notAcceptable = await http.SendAsync(xmlRequest);
            await AssertProblem(notAcceptable, HttpStatusCode.NotAcceptable, "not-acceptable");

            // ── Шаг 8. Сохранение отчёта (FR-11); пустое имя отклоняется ─────────────────────────
            using var blankName = await PostJson(http, "/api/v1/reports",
                new { name = "  ", airportId = 1, dateFrom = "2026-06-10", dateTo = "2026-06-10" });
            await AssertProblem(blankName, HttpStatusCode.BadRequest, "validation-error");

            using var created = await PostJson(http, "/api/v1/reports",
                new { name = "E2E: Sheremetyevo, 10 June", airportId = 1, dateFrom = "2026-06-10", dateTo = "2026-06-10" });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var report = JsonDocument.Parse(await created.Content.ReadAsStringAsync()).RootElement;
            reportId = report.GetProperty("id").GetInt32();
            Assert.Equal($"/api/v1/reports/{reportId}", created.Headers.Location?.ToString());
            Assert.Equal("Sheremetyevo", report.GetProperty("airportName").GetString());

            // ── Шаг 9. Отчёт виден в списке и по адресу из Location (FR-12) ──────────────────────
            var list = await GetJson(http, "/api/v1/reports");
            Assert.Contains(list.GetProperty("items").EnumerateArray(), r => r.GetProperty("id").GetInt32() == reportId);
            var byLocation = await GetJson(http, created.Headers.Location!.ToString());
            Assert.Equal(reportId, byLocation.GetProperty("id").GetInt32());

            // ── Шаг 10. Удаление отчёта; повторное обращение — 404 ───────────────────────────────
            using var deleted = await http.DeleteAsync($"/api/v1/reports/{reportId}");
            Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
            using var gone = await http.GetAsync($"/api/v1/reports/{reportId}");
            await AssertProblem(gone, HttpStatusCode.NotFound, "not-found");
            reportId = null;

            // ── Шаг 11. Выход (FR-03): токен больше не принимается ───────────────────────────────
            using var logout = await http.DeleteAsync("/api/v1/sessions/current");
            Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
            using var afterLogout = await http.GetAsync("/api/v1/reports");
            await AssertProblem(afterLogout, HttpStatusCode.Unauthorized, "unauthorized");
            token = null;
        }
        finally
        {
            // Если тест упал на середине, созданный отчёт не должен остаться в БД стенда
            if (reportId is not null && token is not null)
            {
                using var cleanup = new HttpClient { BaseAddress = http.BaseAddress };
                cleanup.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
                try { await cleanup.DeleteAsync($"/api/v1/reports/{reportId}"); } catch { /* стенд всё равно удаляется */ }
            }
        }
    }

    private static async Task<JsonElement> GetJson(HttpClient http, string url)
    {
        using var response = await http.GetAsync(url);
        Assert.True(response.StatusCode == HttpStatusCode.OK,
            $"GET {url}: ожидался 200, получен {(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    private static Task<HttpResponseMessage> PostJson<T>(HttpClient http, string url, T body) =>
        http.PostAsJsonAsync(url, body);

    /// <summary>Ответ об ошибке в формате Problem Details (RFC 9457): код, тип содержимого, поле type и status.</summary>
    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string typeSlug)
    {
        var text = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == status, $"ожидался {(int)status}, получен {(int)response.StatusCode}: {text}");
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = JsonDocument.Parse(text).RootElement;
        Assert.EndsWith("/" + typeSlug, problem.GetProperty("type").GetString());
        Assert.Equal((int)status, problem.GetProperty("status").GetInt32());
    }
}
