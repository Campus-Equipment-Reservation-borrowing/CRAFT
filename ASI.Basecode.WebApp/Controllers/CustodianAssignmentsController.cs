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
using System;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Admin → Custodian Assignments: which custodian covers which borrowers, locations and
    /// equipment categories, plus reassigning open reservations.
    /// </summary>
    [Authorize(Roles = RoleCodes.Admin)]
    public class CustodianAssignmentsController : ControllerBase<CustodianAssignmentsController>
    {
        private readonly ICustodianAssignmentService _assignments;

        public CustodianAssignmentsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ICustodianAssignmentService assignments)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._assignments = assignments;
        }

        public IActionResult Index()
        {
            try
            {
                return this.View(this._assignments.GetIndex());
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Index");
                this.TempData["ErrorMessage"] = "Unable to load custodian assignments.";
                return this.View(new CustodianAssignmentIndexViewModel());
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Assign(string scopeType, long custodianUserId, long targetId, string returnUrl = null)
        {
            var (success, error) = this._assignments.Assign(scopeType, custodianUserId, targetId);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Custodian assigned." : error;
            return this.BackTo(returnUrl);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Unassign(long id, string returnUrl = null)
        {
            var (success, error) = this._assignments.Unassign(id);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Assignment removed." : error;
            return this.BackTo(returnUrl);
        }

        /// <summary>Monitoring dashboard: route an open reservation to a custodian or back to the pool.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reassign(long reservationId, long? custodianUserId)
        {
            var (success, error) = this._assignments.Reassign(reservationId, custodianUserId);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Reservation reassigned." : error;
            return this.RedirectToAction("Dashboard", "Monitoring");
        }

        private IActionResult BackTo(string returnUrl)
        {
            return !string.IsNullOrEmpty(returnUrl) && this.Url.IsLocalUrl(returnUrl)
                ? this.Redirect(returnUrl)
                : this.RedirectToAction(nameof(this.Index));
        }
    }
}
