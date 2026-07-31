using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace PrinzipPriceChecker.Tests.Fakes;

/// <summary>
/// Минимальный MVC-контекст для юнит тестов контроллеров: без него у ControllerBase
/// нет HttpContext, а значит и ProblemDetailsFactory, который нужен ValidationProblem().
/// </summary>
internal static class MvcTestServices
{
    private static readonly IServiceProvider Services = Build();

    public static TController WithContext<TController>(TController controller)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { RequestServices = Services },
        };

        return controller;
    }

    private static IServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMvcCore();

        return services.BuildServiceProvider();
    }
}
