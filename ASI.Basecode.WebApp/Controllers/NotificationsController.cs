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

        public IActionResult Index(string search, string status, string type, int page = 1)
        {
            var id = this._currentUser.GetActorUserId();
            if (!id.HasValue)
            {
                this.TempData["ErrorMessage"] = "Please sign in to view notifications.";
                return this.RedirectToAction("Login", "Account");
            }

            var model = this._service.GetNotifications(id.Value, search, status, type, page, 10);
            this.ViewBag.Search = search;
            this.ViewBag.Status = model.Status;
            this.ViewBag.Type = model.Type;

            return this.View(model);
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
