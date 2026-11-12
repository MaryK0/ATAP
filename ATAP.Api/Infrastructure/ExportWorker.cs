using ATAP.BusinessLogic.Domain;

namespace ATAP.Api.Infrastructure;

/// <summary>Durable DB queue, single worker per API process, no fire-and-forget request tasks.</summary>
public sealed class ExportWorker(IServiceScopeFactory scopes, ILogger<ExportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IExportRepository>();
                var job = await repository.ClaimAsync(stoppingToken);
                if (job is null) { await Task.Delay(500, stoppingToken); continue; }
                try
                {
                    using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                    timeout.CancelAfter(TimeSpan.FromMinutes(2));
                    var service = scope.ServiceProvider.GetRequiredService<PlatformService>();
                    var bytes = await service.ExportCsvAsync(job.AirportId, new(job.DateFrom, job.DateTo), timeout.Token);
                    await repository.CompleteAsync(job.Id, bytes, null, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception)
                {
                    logger.LogError(exception, "CSV export {ExportId} failed", job.Id);
                    await repository.CompleteAsync(job.Id, null, "Не удалось сформировать CSV. Повторите выгрузку.", stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Export queue is unavailable; retrying");
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); } catch (OperationCanceledException) { break; }
            }
        }
    }
}
