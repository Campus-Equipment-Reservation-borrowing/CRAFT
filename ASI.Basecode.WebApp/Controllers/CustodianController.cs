using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    [Authorize(Roles = RoleCodes.Staff)]
    public class CustodianController : ControllerBase<CustodianController>
    {
        private readonly ICustodianService _custodianService;
        private readonly IDashboardService _dashboardService;

        public CustodianController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ICustodianService custodianService,
            IDashboardService dashboardService)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._custodianService = custodianService;
            this._dashboardService = dashboardService;
        }

        /// <summary>
        /// [3.0] Custodian dashboard. Route alias at /Custodian as well.
        /// </summary>
        public IActionResult Dashboard()
        {
            try
            {
                return this.View(this._dashboardService.GetCustodianDashboard());
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Dashboard");
                this.TempData["ErrorMessage"] = "Unable to load the dashboard.";
                return this.View(new CustodianDashboardViewModel());
            }
        }

        public IActionResult Index() => this.RedirectToAction(nameof(this.Dashboard));

        public IActionResult Reservations(string search, long? status, int page = 1, int pageSize = 10)
        {
            try
            {
                var model = this._custodianService.GetReservationsIndex(search, status, page, pageSize);
                this.ViewBag.StatusOptions = this.ToSelectList(model.StatusOptions, model.StatusId, "All statuses");
                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Reservations");
                this.TempData["ErrorMessage"] = "Unable to load the reservation list.";
                return this.View(new CustodianReservationsIndexViewModel { Page = new Data.PagedResult<CustodianReservationRowViewModel>() });
            }
        }

        public IActionResult Review(long id)
        {
            var model = this._custodianService.GetReservationReview(id);
            if (model == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.CustodianReservationNotFound;
                return this.RedirectToAction(nameof(this.Reservations));
            }
            return this.View(model);
        }

        /// <summary>
        /// "Claim / Assign to me": takes an unassigned reservation from the shared pool.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleCodes.Technician)]
        public IActionResult Claim(long id, string returnTo = null)
        {
            var (success, error) = this._custodianService.Claim(id);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Reservation assigned to you." : error;
            return string.Equals(returnTo, "review", StringComparison.OrdinalIgnoreCase)
                ? this.RedirectToAction(nameof(this.Review), new { id })
                : this.RedirectToAction(nameof(this.Reservations));
        }

        /// <summary>
        /// Scan-to-release / scan-to-return: resolves a scanned (or typed) asset code or
        /// reservation number. A single actionable match opens directly.
        /// </summary>
        [HttpGet]
        public IActionResult Scan(string code)
        {
            var result = this._custodianService.ResolveScan(code);
            if (result.Matches.Count == 1)
            {
                var match = result.Matches[0];
                return match.Kind switch
                {
                    "RELEASE" => this.RedirectToAction(nameof(this.Release), new { id = match.Id }),
                    "RETURN" => this.RedirectToAction("Create", "Returns", new { id = match.Id }),
                    _ => this.RedirectToAction(nameof(this.Review), new { id = match.Id }),
                };
            }

            return this.View(result);
        }

        /// <summary>
        /// Custodian-side cancel (no-show, item withdrawn, duplicate) with a required reason.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(long id, string reason)
        {
            var (success, error) = this._custodianService.Cancel(id, reason);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Reservation cancelled." : error;
            return success
                ? this.RedirectToAction(nameof(this.Reservations))
                : this.RedirectToAction(nameof(this.Review), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Approve(long id, string notes)
        {
            var (success, error) = this._custodianService.Approve(id, notes);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Reservation approved.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }
            return this.RedirectToAction(nameof(this.Reservations));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reject(long id, string reason)
        {
            var (success, error) = this._custodianService.Reject(id, reason);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Reservation rejected.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }
            return this.RedirectToAction(nameof(this.Reservations));
        }

        public IActionResult Release(long id)
        {
            var context = this._custodianService.GetReleaseContext(id);
            if (context == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.CustodianReservationNotFound;
                return this.RedirectToAction(nameof(this.Reservations));
            }
            if (!context.CanRelease)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.CustodianReleaseNotApproved;
                return this.RedirectToAction(nameof(this.Reservations));
            }
            this.ViewBag.ConditionOptions = this.ToSelectList(context.ConditionOptions, null, "-- Select condition --");
            this.ViewBag.Context = context;
            return this.View(new ReleaseFormViewModel { ReservationId = id, ReleasedQuantity = context.ApprovedQuantity });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Release(ReleaseFormViewModel form)
        {
            if (!this.ModelState.IsValid)
            {
                var ctx = this._custodianService.GetReleaseContext(form.ReservationId);
                this.ViewBag.ConditionOptions = this.ToSelectList(ctx.ConditionOptions, form.ReleaseConditionId, "-- Select condition --");
                this.ViewBag.Context = ctx;
                return this.View(form);
            }

            var (success, error, _) = this._custodianService.Release(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                var ctx = this._custodianService.GetReleaseContext(form.ReservationId);
                this.ViewBag.ConditionOptions = this.ToSelectList(ctx.ConditionOptions, form.ReleaseConditionId, "-- Select condition --");
                this.ViewBag.Context = ctx;
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "Equipment released successfully.";
            return this.RedirectToAction(nameof(this.Borrowings));
        }

        public IActionResult Borrowings(string search, int page = 1, int pageSize = 10)
        {
            try
            {
                var model = this._custodianService.GetActiveBorrowings(search, page, pageSize);
                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Borrowings");
                this.TempData["ErrorMessage"] = "Unable to load active borrowings.";
                return this.View(new ActiveBorrowingsIndexViewModel { Page = new Data.PagedResult<ActiveBorrowingRowViewModel>() });
            }
        }

        private SelectList ToSelectList(IEnumerable<LookupViewModel> options, long? selected, string emptyText)
        {
            var items = options.Select(o => new { Id = o.Id.ToString(), o.Text }).ToList();
            var withEmpty = new[] { new { Id = string.Empty, Text = emptyText } }.Concat(items);
            return new SelectList(withEmpty, "Id", "Text", selected?.ToString());
        }
    }
}
