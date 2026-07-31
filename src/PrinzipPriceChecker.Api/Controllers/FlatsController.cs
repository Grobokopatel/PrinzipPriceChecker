using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Contracts;
using PrinzipPriceChecker.Api.Data;
using PrinzipPriceChecker.Api.Services;

namespace PrinzipPriceChecker.Api.Controllers;

[ApiController]
[Route("api/flats")]
[Tags("Квартиры и цены")]
[Produces("application/json")]
public class FlatsController(
    AppDbContext db,
    PriceMonitor monitor,
    ManualPriceService manualPrice) : ControllerBase
{
    /// <summary>Актуальные цены квартир, на которые оформлены подписки, и ссылки на них.</summary>
    /// <remarks>
    /// Возвращает последние известные сервису цены.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<FlatPriceResponse[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FlatPriceResponse[]>> GetFlatPrices(
        CancellationToken cancellationToken)
    {
        var flats = await db.TrackedFlats
            .Where(flat => flat.Subscriptions.Count > 0)
            .OrderBy(flat => flat.Id)
            .ToListAsync(cancellationToken);

        return Ok(flats.Select(FlatPriceResponse.From).ToArray());
    }

    /// <summary>История цен квартиры, новые записи первыми.</summary>
    [HttpGet("{flatId:int}/history")]
    [ProducesResponseType<PriceHistoryEntryResponse[]>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PriceHistoryEntryResponse[]>> GetPriceHistory(
        int flatId,
        CancellationToken cancellationToken)
    {
        var flatExists = await db.TrackedFlats.AnyAsync(flat => flat.Id == flatId, cancellationToken);

        if (!flatExists)
        {
            return NotFound();
        }

        var history = await db.PriceHistory
            .Where(entry => entry.TrackedFlatId == flatId)
            .OrderByDescending(entry => entry.Id)
            .ToListAsync(cancellationToken);

        return Ok(history.Select(PriceHistoryEntryResponse.From).ToArray());
    }

    /// <summary>Проверить цену квартиры прямо сейчас.</summary>
    /// <remarks>
    /// Сравнивает сохранённую цену с ценой на сайте и при расхождении рассылает уведомления.
    /// </remarks>
    [HttpPost("{flatId:int}/check")]
    [ProducesResponseType<FlatCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FlatCheckResponse>> CheckFlat(
        int flatId,
        CancellationToken cancellationToken)
    {
        var result = await monitor.CheckFlatAsync(flatId, cancellationToken);

        return result is null
            ? NotFound()
            : Ok(FlatCheckResponse.From(result));
    }

    /// <summary>
    /// Проверить цены всех отслеживаемых квартир прямо сейчас.
    /// Сравнивает сохранённую цену с ценой на сайте и при расхождении рассылает уведомления.
    /// </summary>
    /// <remarks>Ручной запуск того же обхода, который выполняет фоновая служба.</remarks>
    [HttpPost("check")]
    [ProducesResponseType<FlatCheckResponse[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<FlatCheckResponse[]>> CheckAllFlats(CancellationToken cancellationToken)
    {
        var results = await monitor.CheckAllAsync(cancellationToken);

        return Ok(results.Select(FlatCheckResponse.From).ToArray());
    }

    /// <summary>Изменить сохранённую цену квартиры.</summary>
    [HttpPut("{flatId:int}/price")]
    [ProducesResponseType<ManualPriceChangeResult>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ManualPriceChangeResult>> SetFlatPrice(
        int flatId,
        SetPriceRequest request,
        CancellationToken cancellationToken)
    {
        if (request.NewPrice <= 0m)
        {
            ModelState.AddModelError(nameof(request.NewPrice), "Цена должна быть больше нуля.");

            return ValidationProblem(ModelState);
        }

        
        var flat = await db.TrackedFlats
            .Include(f => f.Subscriptions)
            .FirstOrDefaultAsync(f => f.Id == flatId, cancellationToken);

        if (flat is null)
        {
            return NotFound();
        }
        
        var result = await manualPrice.SetPriceAsync(
            flat,
            request.NewPrice,
            request.SendNotification,
            cancellationToken);
        
        return result is null
            ? NotFound()
            : Ok(result);
    }
}
