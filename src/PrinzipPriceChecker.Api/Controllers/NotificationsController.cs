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
    /// <summary>Журнал отправленных уведомлений.</summary>
    /// <remarks>
    /// Показывает письма, отправленные подписчикам или неполучившиеся отправить.
    /// </remarks>
    [HttpGet]
    [ProducesResponseType<NotificationResponse[]>(StatusCodes.Status200OK)]
    public async Task<ActionResult<NotificationResponse[]>> GetNotifications(
        int? take,
        CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(take ?? 50, 1, 500);

        var notifications = await db.Notifications
            .OrderByDescending(notification => notification.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

        return Ok(notifications.Select(NotificationResponse.From).ToArray());
    }
}
