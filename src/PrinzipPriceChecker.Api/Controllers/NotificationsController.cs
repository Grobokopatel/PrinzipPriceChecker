using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PrinzipPriceChecker.Api.Contracts;
using PrinzipPriceChecker.Api.Data;

namespace PrinzipPriceChecker.Api.Controllers;

[ApiController]
[Route("api/notifications")]
[Tags("Уведомления")]
[Produces("application/json")]
public class NotificationsController(AppDbContext db) : ControllerBase
{
    /// <summary>Журнал отправленных на почту уведомлений.</summary>
    /// <remarks>
    /// Показывает письма, отправленные подписчикам или неполучившиеся отправить.
    /// </remarks>
    /// <param name="take">Сколько записей вернуть, новые первыми. Ограничивается диапазоном 1-500.</param>
    [HttpGet]
    [ProducesResponseType<NotificationResponse[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse[]>> GetNotifications(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var limit = Math.Clamp(take, 1, 500);

        var notifications = await db.Notifications
            .OrderByDescending(notification => notification.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(notifications.Select(NotificationResponse.From).ToArray());
    }
}
