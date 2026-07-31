using Microsoft.AspNetCore.Mvc;

namespace PrinzipPriceChecker.Api.Controllers;

[ApiController]
[Tags("Служебные")]
public class ServiceController : ControllerBase
{
    [HttpGet("/")]
    [ApiExplorerSettings(IgnoreApi = true)]
    public IActionResult Index() => Redirect("/swagger");
}
