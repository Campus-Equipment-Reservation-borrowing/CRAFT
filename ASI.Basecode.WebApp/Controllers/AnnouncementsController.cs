using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Admin broadcast announcements to role audiences (in-app notification + dashboard banner).
    /// </summary>
    [Authorize(Roles = RoleCodes.Admin)]
    public class AnnouncementsController : ControllerBase<AnnouncementsController>
    {
        private readonly ICommunicationService _communication;

        public AnnouncementsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ICommunicationService communication)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._communication = communication;
        }

        public IActionResult Index()
        {
            return this.View(this._communication.GetAnnouncements());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Publish(AnnouncementFormViewModel form)
        {
            var (success, error) = this._communication.PublishAnnouncement(form);
            if (!success)
            {
                var model = this._communication.GetAnnouncements();
                model.Form = form;
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(nameof(this.Index), model);
            }

            this.TempData["SuccessMessage"] = "Announcement published.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(long id)
        {
            var (success, error) = this._communication.DeleteAnnouncement(id);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Announcement deleted." : error;
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
