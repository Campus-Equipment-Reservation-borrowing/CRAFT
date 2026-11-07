using ASI.Basecode.Resources.Constants;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Home Controller — the public landing page and the role router.
    /// </summary>
    public class HomeController : ControllerBase<HomeController>
    {
        private readonly ASI.Basecode.Services.Interfaces.ICommunicationService _communication;

        public HomeController(IHttpContextAccessor httpContextAccessor,
                              ILoggerFactory loggerFactory,
                              IConfiguration configuration,
                              ASI.Basecode.Services.Interfaces.ICommunicationService communication,
                              IMapper mapper = null) : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._communication = communication;
        }

        /// <summary>
        /// [1.0 Landing] — public when signed out; otherwise routes to the role dashboard.
        /// </summary>
        public IActionResult Index()
        {
            if (this.User?.Identity?.IsAuthenticated != true)
            {
                return this.View();
            }

            return this.RedirectToRoleHome(this.User.FindFirst(ClaimTypes.Role)?.Value);
        }

        /// <summary>Shown to non-administrators while a MAINTENANCE announcement is active.</summary>
        [AllowAnonymous]
        public IActionResult Maintenance()
        {
            var announcement = this._communication.GetActiveMaintenanceAnnouncement();
            if (announcement == null)
            {
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(announcement);
        }

        /// <summary>Safe error page used by the production exception handler.</summary>
        [AllowAnonymous]
        public IActionResult Error()
        {
            return this.View(new ASI.Basecode.WebApp.Models.ErrorViewModel
            {
                RequestId = System.Diagnostics.Activity.Current?.Id ?? this.HttpContext.TraceIdentifier,
            });
        }

        private IActionResult RedirectToRoleHome(string roleCode)
        {
            switch (roleCode)
            {
                case RoleCodes.Admin:
                    return this.RedirectToAction("Dashboard", "Monitoring");
                case RoleCodes.Technician:
                    return this.RedirectToAction("Dashboard", "Custodian");
                case RoleCodes.Student:
                case RoleCodes.Faculty:
                    return this.RedirectToAction("Dashboard", "Borrower");
                default:
                    return this.View("Index");
            }
        }
    }
}
