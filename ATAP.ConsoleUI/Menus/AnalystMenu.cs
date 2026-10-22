using Microsoft.Extensions.Logging;
using ATAP.BusinessLogic.DTO;
using ATAP.BusinessLogic.Interfaces.Services;
using ATAP.ConsoleUI.Helpers;

namespace ATAP.ConsoleUI.Menus;

public class AnalystMenu : IMenu
{
    private readonly IAnalyticsService _analyticsService;
    private readonly IMapService _mapService;
    private readonly ISavedReportService _savedReportService;
    private readonly SessionContext _session;
    private readonly ILogger<AnalystMenu> _logger;
    private readonly int _maxFlightsPreview;
    private readonly int _maxReportPreview;
    private CongestionReportDto? _lastReport;

    public AnalystMenu(
        IAnalyticsService analyticsService,
        IMapService mapService,
        ISavedReportService savedReportService,
        SessionContext session,
        AppSettings appSettings,
        ILogger<AnalystMenu> logger)
    {
        _analyticsService = analyticsService;
        _mapService = mapService;
        _savedReportService = savedReportService;
        _session = session;
        _logger = logger;
        _maxFlightsPreview = appSettings.MaxFlightsPreview;
        _maxReportPreview = appSettings.MaxReportPreview;
    }

    public async Task ShowAsync()
    {
        while (_session.IsLoggedIn && !_session.ExitRequested)
        {
            Console.Clear();
            ConsoleHelper.PrintHeader($"ATAP — Аналитик: {_session.CurrentUser!.Username}");
            Console.WriteLine("  1. Список аэропортов");
            Console.WriteLine("  2. Анализ загруженности аэропорта");
            Console.WriteLine("  3. Выгрузить последний отчёт в CSV");
            Console.WriteLine("  4. Просмотр текущих рейсов");
            Console.WriteLine("  5. Сменить папку логирования");
            Console.WriteLine("  6. Сохранить текущий отчёт");
            Console.WriteLine("  7. Мои сохранённые отчёты");
            Console.WriteLine("  0. Выйти из аккаунта");
            Console.Write("\nВыберите действие: ");

            switch (Console.ReadLine()?.Trim())
            {
                case "1": await ShowAirportsAsync(); break;
                case "2": await ShowCongestionReportAsync(); break;
                case "3": await ExportCsvAsync(); break;
                case "4": await ShowFlightsAsync(); break;
                case "5": ChangeLogFolder(); break;
                case "6": await SaveReportAsync(); break;
                case "7": await ShowSavedReportsAsync(); break;
                case "0":
                    _logger.LogInformation("Аналитик {Username} вышел из системы", _session.CurrentUser.Username);
                    _session.CurrentUser = null;
                    break;
                default:
                    _logger.LogDebug("Аналитик {Username} ввёл неверный пункт меню",
                        _session.CurrentUser.Username);
                    ConsoleHelper.PrintError("Неверный выбор");
                    ConsoleHelper.Pause();
                    break;
            }
        }
    }

    private async Task SaveReportAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Сохранить отчёт");

        if (_lastReport == null)
        {
            ConsoleHelper.PrintError("Нет сформированного отчёта. Сначала выполните анализ (пункт 2).");
            ConsoleHelper.Pause();
            return;
        }

        Console.WriteLine($"Отчёт: аэропорт ID={_lastReport.AirportId}, " +
                          $"вылеты={_lastReport.DepartureCount}, прилёты={_lastReport.ArrivalCount}\n");
        Console.Write("Название отчёта: ");
        var name = Console.ReadLine()?.Trim() ?? "";

        Console.Write("Дата начала периода (гггг-мм-дд): ");
        if (!DateOnly.TryParse(Console.ReadLine(), out var dateFrom))
        {
            ConsoleHelper.PrintError("Неверный формат даты");
            ConsoleHelper.Pause();
            return;
        }

        Console.Write("Дата окончания периода (гггг-мм-дд): ");
        if (!DateOnly.TryParse(Console.ReadLine(), out var dateTo))
        {
            ConsoleHelper.PrintError("Неверный формат даты");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            await _savedReportService.SaveAsync(_session.CurrentUser!.UserId, new CreateSavedReportDto
            {
                Name      = name,
                AirportId = _lastReport.AirportId,
                DateFrom  = dateFrom,
                DateTo    = dateTo
            });
            _logger.LogInformation("Аналитик {Username} сохранил отчёт {Name}", _session.CurrentUser.Username, name);
            ConsoleHelper.PrintSuccess($"Отчёт «{name}» сохранён.");
        }
        catch (ArgumentException ex)
        {
            ConsoleHelper.PrintError(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при сохранении отчёта аналитика {Username}", _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowSavedReportsAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Мои сохранённые отчёты");

        try
        {
            var reports = await _savedReportService.GetByUserAsync(_session.CurrentUser!.UserId);

            if (reports.Count == 0)
            {
                Console.WriteLine("Сохранённых отчётов нет.");
                ConsoleHelper.Pause();
                return;
            }

            Console.WriteLine($"{"ID",-5} {"Название",-25} {"Аэропорт",-25} {"Период",-25} {"Сохранён"}");
            Console.WriteLine(new string('-', 95));
            foreach (var r in reports)
                Console.WriteLine($"{r.Id,-5} {r.Name,-25} {r.AirportName,-25} {r.DateFrom:yyyy-MM-dd}–{r.DateTo:yyyy-MM-dd}  {r.CreatedAt:yyyy-MM-dd HH:mm}");

            Console.WriteLine();
            Console.Write("Открыть отчёт по ID (Enter — назад): ");
            var input = Console.ReadLine()?.Trim();
            if (!int.TryParse(input, out var id)) return;

            var selected = reports.FirstOrDefault(r => r.Id == id);
            if (selected == null) { ConsoleHelper.PrintError("Отчёт не найден"); ConsoleHelper.Pause(); return; }

            var report = await _analyticsService.GetCongestionAsync(
                selected.AirportId,
                selected.DateFrom.ToDateTime(TimeOnly.MinValue),
                selected.DateTo.ToDateTime(TimeOnly.MinValue).AddDays(1));

            _lastReport = report;
            Console.Clear();
            ConsoleHelper.PrintHeader($"Отчёт: {selected.Name}");
            Console.WriteLine($"{"Аэропорт:",-20} {selected.AirportName}");
            Console.WriteLine($"{"Период:",-20} {selected.DateFrom:yyyy-MM-dd} — {selected.DateTo:yyyy-MM-dd}");
            Console.WriteLine($"{"Вылеты:",-20} {report.DepartureCount}");
            Console.WriteLine($"{"Прилёты:",-20} {report.ArrivalCount}");
            Console.WriteLine($"{"Всего движений:",-20} {report.DepartureCount + report.ArrivalCount}");
            ConsoleHelper.PrintSuccess("Отчёт загружен. Используйте пункт 3 для выгрузки CSV.");
            _logger.LogInformation("Аналитик {Username} открыл сохранённый отчёт {Name}",
                _session.CurrentUser!.Username, selected.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке сохранённых отчётов аналитика {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private void ChangeLogFolder()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Смена папки логирования");
        Console.WriteLine($"Текущий путь: {LogConfigurator.CurrentLogPath}\n");
        Console.Write("Новый путь к лог-файлу: ");
        var input = Console.ReadLine()?.Trim() ?? "";
        if (LogConfigurator.TryChange(input, out var error))
        {
            _logger.LogInformation("Аналитик {Username} изменил путь к лог-файлу на {Path}",
                _session.CurrentUser!.Username, LogConfigurator.CurrentLogPath);
            ConsoleHelper.PrintSuccess($"Лог-файл: {LogConfigurator.CurrentLogPath}");
        }
        else
        {
            ConsoleHelper.PrintError(error);
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowAirportsAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Список аэропортов");
        Console.WriteLine("Загрузка...\n");
        _logger.LogInformation("Аналитик {Username} запросил список аэропортов",
            _session.CurrentUser!.Username);

        try
        {
            var airports = await _mapService.GetAirportsAsync();
            if (airports.Count == 0)
            {
                Console.WriteLine("Аэропорты не найдены.");
                _logger.LogWarning("Список аэропортов пуст");
            }
            else
            {
                Console.WriteLine($"{"ID",-6} {"ICAO",-8} {"Название",-40} {"Широта",-12} {"Долгота"}");
                Console.WriteLine(new string('-', 78));
                foreach (var a in airports)
                    Console.WriteLine($"{a.Id,-6} {a.IcaoCode,-8} {a.Name,-40} {a.Latitude,-12:F4} {a.Longitude:F4}");
                Console.WriteLine($"\nВсего аэропортов: {airports.Count}");
                _logger.LogInformation("Аналитику {Username} показано {Count} аэропортов",
                    _session.CurrentUser!.Username, airports.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке списка аэропортов для аналитика {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowCongestionReportAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Анализ загруженности аэропорта");

        Console.Write("ID аэропорта (см. список аэропортов): ");
        if (!int.TryParse(Console.ReadLine(), out var airportId))
        {
            _logger.LogWarning("Аналитик {Username} ввёл неверный ID аэропорта",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Неверный ID аэропорта");
            ConsoleHelper.Pause();
            return;
        }

        Console.Write("Дата начала периода (гггг-мм-дд): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var start))
        {
            _logger.LogWarning("Аналитик {Username} ввёл неверную дату начала периода",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Неверный формат даты. Используйте: 2024-01-01");
            ConsoleHelper.Pause();
            return;
        }

        Console.Write("Дата окончания периода (гггг-мм-дд): ");
        if (!DateTime.TryParse(Console.ReadLine(), out var end))
        {
            _logger.LogWarning("Аналитик {Username} ввёл неверную дату окончания периода",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Неверный формат даты. Используйте: 2024-01-31");
            ConsoleHelper.Pause();
            return;
        }

        Console.WriteLine("\nЗапрос к витрине mart_airport_congestion...\n");
        _logger.LogInformation(
            "Аналитик {Username} запросил отчёт загруженности: аэропорт {AirportId}, период {Start:yyyy-MM-dd}–{End:yyyy-MM-dd}",
            _session.CurrentUser!.Username, airportId, start, end);

        try
        {
            var report = await _analyticsService.GetCongestionAsync(airportId, start, end);
            _lastReport = report;

            Console.WriteLine($"{"Аэропорт ID:",-20} {report.AirportId}");
            Console.WriteLine($"{"Период:",-20} {start:yyyy-MM-dd} — {end:yyyy-MM-dd}");
            Console.WriteLine($"{"Вылеты:",-20} {report.DepartureCount}");
            Console.WriteLine($"{"Прилёты:",-20} {report.ArrivalCount}");
            Console.WriteLine($"{"Всего движений:",-20} {report.DepartureCount + report.ArrivalCount}");

            if (report.Entries.Count > 0)
            {
                Console.WriteLine();
                Console.WriteLine($"{"flight_id",-10} {"callsign",-12} {"entry_time",-20} {"exit_time",-20} {"dur_sec",8} {"is_dep",-7} {"is_arr",-7} {"max_alt",8} {"min_alt",8}");
                Console.WriteLine(new string('-', 106));
                foreach (var e in report.Entries.Take(_maxReportPreview))
                {
                    Console.WriteLine(
                        $"{e.FlightId,-10} {e.Callsign,-12} {e.EntryTime,-20:yyyy-MM-dd HH:mm} {e.ExitTime,-20:yyyy-MM-dd HH:mm} {e.DurationSeconds,8} {e.IsDeparture,-7} {e.IsArrival,-7} {e.MaxAltitudeInZone,8} {e.MinAltitudeInZone,8}");
                }
                if (report.Entries.Count > _maxReportPreview)
                    Console.WriteLine($"... и ещё {report.Entries.Count - _maxReportPreview} записей (все попадут в CSV)");
            }

            _logger.LogInformation(
                "Отчёт сформирован для аналитика {Username}: аэропорт {AirportId}, вылеты={Dep}, прилёты={Arr}",
                _session.CurrentUser!.Username, airportId, report.DepartureCount, report.ArrivalCount);
            ConsoleHelper.PrintSuccess("Отчёт сформирован. Используйте пункт 3 для выгрузки CSV.");
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Некорректные параметры запроса отчёта от аналитика {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка параметров: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при построении отчёта для аналитика {Username}, аэропорт {AirportId}",
                _session.CurrentUser!.Username, airportId);
            ConsoleHelper.PrintError($"Ошибка запроса к БД: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ExportCsvAsync()
    {
        if (_lastReport == null)
        {
            _logger.LogWarning("Аналитик {Username} попытался выгрузить CSV без предварительного запроса отчёта",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError("Нет данных для выгрузки. Сначала выполните анализ (пункт 2).");
            ConsoleHelper.Pause();
            return;
        }

        try
        {
            var bytes = await _analyticsService.ExportToCsvAsync(_lastReport);
            var fileName = $"congestion_airport{_lastReport.AirportId}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            await File.WriteAllBytesAsync(fileName, bytes);
            var fullPath = Path.GetFullPath(fileName);
            _logger.LogInformation(
                "Аналитик {Username} выгрузил CSV отчёт: аэропорт {AirportId}, файл {FileName}",
                _session.CurrentUser!.Username, _lastReport.AirportId, fullPath);
            ConsoleHelper.PrintSuccess($"Файл сохранён: {fullPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при экспорте CSV для аналитика {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка экспорта: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }

    private async Task ShowFlightsAsync()
    {
        Console.Clear();
        ConsoleHelper.PrintHeader("Текущие рейсы");
        Console.WriteLine("Загрузка...\n");
        _logger.LogInformation("Аналитик {Username} запросил список текущих рейсов",
            _session.CurrentUser!.Username);

        try
        {
            var flights = await _mapService.GetCurrentFlightsAsync();
            if (flights.Count == 0)
            {
                Console.WriteLine("Данные о рейсах отсутствуют.");
                _logger.LogWarning("В БД нет данных о текущих рейсах");
            }
            else
            {
                Console.WriteLine($"{"ID",-8} {"Позывной",-14} {"Широта",-12} {"Долгота",-12} {"Высота (м)",12}");
                Console.WriteLine(new string('-', 62));
                foreach (var f in flights.Take(_maxFlightsPreview))
                    Console.WriteLine($"{f.Id,-8} {f.Callsign,-14} {f.Latitude,-12:F4} {f.Longitude,-12:F4} {f.Altitude,12}");
                if (flights.Count > _maxFlightsPreview)
                    Console.WriteLine($"... и ещё {flights.Count - _maxFlightsPreview} рейсов");
                Console.WriteLine($"\nВсего активных рейсов: {flights.Count}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при загрузке рейсов для аналитика {Username}",
                _session.CurrentUser!.Username);
            ConsoleHelper.PrintError($"Ошибка: {ex.Message}");
        }
        ConsoleHelper.Pause();
    }
}
