using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.ViewComponents
{
    /// <summary>
    /// Header bell with the signed-in user's unread notification count (all roles).
    /// </summary>
    public class NotificationBadgeViewComponent : ViewComponent
    {
        private readonly INotificationDispatcher _notifications;
        private readonly ILogger<NotificationBadgeViewComponent> _logger;

        public NotificationBadgeViewComponent(INotificationDispatcher notifications, ILogger<NotificationBadgeViewComponent> logger)
        {
            this._notifications = notifications;
            this._logger = logger;
        }

        public IViewComponentResult Invoke()
        {
            var unread = 0;
            if (long.TryParse(this.UserClaimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                try
                {
                    unread = this._notifications.GetUnreadCount(userId);
                }
                catch (Exception ex)
                {
                    // The header must render even if the count can't be read.
                    this._logger.LogWarning(ex, "Unable to read unread notification count.");
                }
            }

            return this.View(unread);
        }
    }
}
