using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.ViewComponents
{
    /// <summary>Active announcements for the signed-in user's role, shown above dashboard content.</summary>
    public class AnnouncementBannerViewComponent : ViewComponent
    {
        private readonly ICommunicationService _communication;
        private readonly ILogger<AnnouncementBannerViewComponent> _logger;

        public AnnouncementBannerViewComponent(ICommunicationService communication, ILogger<AnnouncementBannerViewComponent> logger)
        {
            this._communication = communication;
            this._logger = logger;
        }

        public IViewComponentResult Invoke()
        {
            var items = new List<AnnouncementRowViewModel>();
            try
            {
                items = this._communication.GetActiveAnnouncementsFor(this.UserClaimsPrincipal?.FindFirst(ClaimTypes.Role)?.Value);
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Unable to load announcements.");
            }

            return this.View(items);
        }
    }
}
