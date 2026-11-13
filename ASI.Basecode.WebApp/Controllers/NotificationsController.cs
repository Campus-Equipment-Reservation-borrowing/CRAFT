using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize]
    public class NotificationsController : ControllerBase<NotificationsController>
    {
        private readonly ISettingsService _service;
        private readonly ICurrentUserService _currentUser;

        public NotificationsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ISettingsService service,
            ICurrentUserService currentUser)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._service = service;
            this._currentUser = currentUser;
        }

        public IActionResult Index(string search, string status, string type)
        {
            var id = this._currentUser.GetActorUserId();
            if (!id.HasValue)
            {
                this.TempData["ErrorMessage"] = "Please sign in to view notifications.";
                return this.RedirectToAction("Login", "Account");
            }

            var model = this._service.GetNotifications(id.Value);
            var items = model.Notifications.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                items = items.Where(n =>
                    (n.Title ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    (n.Message ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase));
            }

            if (string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(n => !n.IsRead);
            }
            else if (string.Equals(status, "read", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(n => n.IsRead);
            }

            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
            {
                items = items.Where(n => string.Equals(n.NotificationType, type, StringComparison.OrdinalIgnoreCase));
            }

            model.Notifications = items.ToList();
            this.ViewBag.Search = search;
            this.ViewBag.Status = string.IsNullOrWhiteSpace(status) ? "all" : status;
            this.ViewBag.Type = string.IsNullOrWhiteSpace(type) ? "all" : type;

            return this.View(model);
        }

        /// <summary>Marks the notification read and goes to what it is about.</summary>
        [HttpGet]
        public IActionResult Open(long id)
        {
            var uid = this._currentUser.GetActorUserId();
            var target = uid.HasValue ? this._service.OpenNotification(id, uid.Value) : null;
            // Only local targets — a stored link must never become an open redirect.
            if (!string.IsNullOrEmpty(target) && this.Url.IsLocalUrl(target))
            {
                return this.LocalRedirect(target);
            }
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkRead(long id)
        {
            var uid = this._currentUser.GetActorUserId();
            if (uid.HasValue)
            {
                this._service.MarkNotificationRead(id, uid.Value);
            }
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult MarkAllRead()
        {
            var uid = this._currentUser.GetActorUserId();
            if (uid.HasValue)
            {
                this._service.MarkAllNotificationsRead(uid.Value);
            }
            this.TempData["SuccessMessage"] = "All notifications marked as read.";
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
