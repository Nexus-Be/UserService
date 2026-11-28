namespace User.Api.Workers;

using User.Application.Services.Seller;

/// <summary>
/// Every few minutes creates the warehouse of approved sellers whose warehouse could not be created at approval time (Fulfillment was down or
/// slow). A seller without a warehouse cannot get a shipping fee or a shipment, so this closes the gap without blocking the approval itself.
/// </summary>
internal sealed class SellerWarehouseRetryWorker(IServiceScopeFactory scopes, ILogger<SellerWarehouseRetryWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopes.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ISellerService>();
                var ready = await service.RetryMissingWarehousesAsync(20, stoppingToken);
                if (ready > 0)
                {
                    logger.LogInformation("Seller warehouse retry created {Count} warehouse(s)", ready);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { logger.LogError(error, "Seller warehouse retry failed"); }
        }
    }
}
