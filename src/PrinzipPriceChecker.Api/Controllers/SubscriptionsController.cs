using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Contracts;
using PrinzipPriceChecker.Api.Data;
using PrinzipPriceChecker.Api.Parsing;
using PrinzipPriceChecker.Api.Services;
using PrinzipPriceChecker.Api.Validation;

namespace PrinzipPriceChecker.Api.Controllers;

[ApiController]
[Route("api/subscriptions")]
[Tags("Подписки")]
[Produces("application/json")]
public class SubscriptionsController(
    AppDbContext db,
    SubscriptionService subscriptions) : ControllerBase
{
    /// <summary>Подписаться на изменение цены квартиры.</summary>
    /// <remarks>
    /// Принимает ссылку на объявление и email.
    /// Цена подтягивается с сайта сразу при подписке.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<SubscriptionResponse>> CreateSubscription(
        CreateSubscriptionRequest request,
        CancellationToken cancellationToken)
    {
        if (!FlatUrl.TryNormalize(request.Url, out var normalizedUrl, out var urlError))
        {
            ModelState.AddModelError(nameof(request.Url), urlError);
        }

        if (!EmailExtensions.TryNormalize(request.Email, out var email, out var emailError))
        {
            ModelState.AddModelError(nameof(request.Email), emailError);
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var outcome = await subscriptions.CreateAsync(normalizedUrl!, email!, cancellationToken);

        switch (outcome.Status)
        {
            case CreateSubscriptionStatus.FlatNotFound:
                return Problem(
                    title: "Квартира не найдена на сайте",
                    detail: outcome.Error,
                    statusCode: StatusCodes.Status404NotFound);

            case CreateSubscriptionStatus.SiteUnavailable:
                return Problem(
                    title: "Сайт сейчас недоступен",
                    detail: $"{outcome.Error} Проверить квартиру не удалось, попробуйте позже.",
                    statusCode: StatusCodes.Status503ServiceUnavailable);

            case CreateSubscriptionStatus.AlreadyExists:
                return Problem(
                    title: "Подписка уже существует",
                    detail: $"Email {email} уже подписан на {normalizedUrl}.",
                    statusCode: StatusCodes.Status409Conflict);
        }

        var response = SubscriptionResponse.From(outcome.Subscription!);

        return CreatedAtAction(nameof(GetSubscription), new { id = response.Id }, response);
    }

    /// <summary>Список оформленных подписок.</summary>
    [HttpGet]
    [ProducesResponseType<SubscriptionResponse[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionResponse[]>> GetSubscriptions(
        CancellationToken cancellationToken)
    {
        var all = await db.Subscriptions
            .Include(s => s.TrackedFlat)
            .OrderBy(s => s.Id)
            .ToListAsync(cancellationToken);

        return Ok(all.Select(SubscriptionResponse.From).ToArray());
    }

    /// <summary>Получить подписку по идентификатору.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType<SubscriptionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SubscriptionResponse>> GetSubscription(
        int id,
        CancellationToken cancellationToken)
    {
        var subscription = await db.Subscriptions
            .Include(s => s.TrackedFlat)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

        return subscription is null
            ? NotFound()
            : Ok(SubscriptionResponse.From(subscription));
    }
}
