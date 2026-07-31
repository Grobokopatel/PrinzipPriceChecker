using Microsoft.Extensions.Options;

namespace PrinzipPriceChecker.Api.Services;

/// <summary>Периодически обходит все отслеживаемые квартиры и проверяет их цены.</summary>
public class PriceMonitorWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<MonitoringOptions> options,
    ILogger<PriceMonitorWorker> logger) : BackgroundService
{
    private readonly MonitoringOptions _options = options.Value;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            logger.LogInformation(
                "Фоновая проверка цен отключена настройкой {Section}:Enabled",
                MonitoringOptions.SectionName);
            return;
        }

        logger.LogInformation(
            "Фоновая проверка цен запущена: интервал {Interval}, первая проверка через {Delay}",
            _options.Interval,
            _options.StartupDelay);

        try
        {
            if (_options.StartupDelay > TimeSpan.Zero)
            {
                await Task.Delay(_options.StartupDelay, stoppingToken);
            }

            using var timer = new PeriodicTimer(_options.Interval);

            do
            {
                await RunOnceAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException)
        {
            // Штатное завершение вместе с приложением.
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var monitor = scope.ServiceProvider.GetRequiredService<PriceMonitor>();

            await monitor.CheckAllAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Ошибка в цикле фоновой проверки цен");
        }
    }
}
