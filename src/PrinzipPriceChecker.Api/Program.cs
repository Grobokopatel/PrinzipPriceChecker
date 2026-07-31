using System.Net;
using System.Reflection;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Data;
using PrinzipPriceChecker.Api.Parsing;
using PrinzipPriceChecker.Api.Services;
using PrinzipPriceChecker.Api.Services.Email;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PrinzipPriceChecker.Api;

public class Program
{
    public static async Task Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        ConfigureServices(builder);

        var app = builder.Build();

        await ApplyMigrationsAsync(app);

        ConfigurePipeline(app);

        await app.RunAsync();
    }

    private static void ConfigureServices(WebApplicationBuilder builder)
    {
        var configuration = builder.Configuration;

        builder.Services.Configure<MonitoringOptions>(
            configuration.GetSection(MonitoringOptions.SectionName));
        builder.Services.Configure<EmailOptions>(
            configuration.GetSection(EmailOptions.SectionName));

        var connectionString = configuration.GetConnectionString("Default")
            ?? "Data Source=data/pricechecker.db";

        EnsureDatabaseDirectoryExists(connectionString, builder.Environment.ContentRootPath);

        builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));

        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<JsonLdFlatParser>();

        ConfigurePriceSource(builder);
        ConfigureEmailSender(builder);

        builder.Services.AddScoped<PriceMonitor>();
        builder.Services.AddScoped<SubscriptionService>();
        builder.Services.AddScoped<ManualPriceService>();

        builder.Services.AddHostedService<PriceMonitorWorker>();

        builder.Services.AddControllers();
        builder.Services.AddSwaggerGen(ConfigureSwagger);
    }

    private static void ConfigureSwagger(SwaggerGenOptions options)
    {
        options.SwaggerDoc("v1", new()
        {
            Title = "Prinzip Price Checker",
            Version = "v1",
            Description = "Сервис слежения за ценами квартир на prinzip.su.",
        });

        options.TagActionsBy(api =>
            api.ActionDescriptor.EndpointMetadata.OfType<ITagsMetadata>().FirstOrDefault()?.Tags.ToList()
            ?? [api.ActionDescriptor.RouteValues["controller"] ?? "API"]);

        // Заголовки и описания операций берутся из XML-комментариев к действиям.
        var xmlPath = Path.Combine(
            AppContext.BaseDirectory,
            $"{Assembly.GetExecutingAssembly().GetName().Name}.xml");

        if (File.Exists(xmlPath))
        {
            options.IncludeXmlComments(xmlPath);
        }
    }

    private static void ConfigurePriceSource(WebApplicationBuilder builder)
    {
        builder.Services
            .AddHttpClient<IFlatPriceSource, PrinzipFlatPriceSource>(
                PrinzipFlatPriceSource.HttpClientName,
                client =>
                {
                    client.Timeout = TimeSpan.FromSeconds(60);

                    client.DefaultRequestHeaders.UserAgent.ParseAdd(
                        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) "
                        + "Chrome/120.0.0.0 Safari/537.36");
                    client.DefaultRequestHeaders.Accept.ParseAdd("text/html,application/xhtml+xml");
                    client.DefaultRequestHeaders.AcceptLanguage.ParseAdd("ru-RU,ru;q=0.9");
                })
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.All,
            });
    }

    private static void ConfigureEmailSender(WebApplicationBuilder builder)
    {
        // Опции читаются напрямую из конфигурации: IOptions<EmailOptions> на этапе сборки
        // контейнера ещё недоступен. Невалидное значение Provider уронит старт, а не уведёт
        // отправку в лог молча.
        var options = builder.Configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>()
            ?? new EmailOptions();

        if (options.Provider == EmailProvider.Smtp)
        {
            builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
        }
        else
        {
            builder.Services.AddScoped<IEmailSender, LoggingEmailSender>();
        }
    }

    private static async Task ApplyMigrationsAsync(WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.MigrateAsync();
    }

    private static void ConfigurePipeline(WebApplication app)
    {
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "Prinzip Price Checker v1");
            options.DocumentTitle = "Prinzip Price Checker";
        });

        app.MapControllers();
    }

    // SQLite не создаёт каталог для файла базы сам, поэтому делаем это до первого обращения.
    private static void EnsureDatabaseDirectoryExists(string connectionString, string contentRootPath)
    {
        var dataSource = new SqliteConnectionStringBuilder(connectionString).DataSource;

        if (string.IsNullOrWhiteSpace(dataSource) || dataSource == ":memory:")
        {
            return;
        }

        var fullPath = Path.IsPathRooted(dataSource)
            ? dataSource
            : Path.Combine(contentRootPath, dataSource);

        var directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
}
