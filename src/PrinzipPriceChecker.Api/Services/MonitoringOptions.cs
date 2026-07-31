namespace PrinzipPriceChecker.Api.Services;

public class MonitoringOptions
{
    public const string SectionName = "Monitoring";

    /// <summary>Включена ли фоновая периодическая проверка цен.</summary>
    public bool Enabled { get; set; } = true;

    /// <summary>Период между проверками всех отслеживаемых квартир.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>Задержка перед первой проверкой после старта приложения.</summary>
    public TimeSpan StartupDelay { get; set; } = TimeSpan.FromSeconds(10);
}
