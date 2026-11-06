using ASI.Basecode.Data;
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
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Borrower reservation screens (Figma: B - Equipment Availability / Reservation Form /
    /// My Reservations / Reservation Details). Thin controller: binds requests, delegates to
    /// IReservationService, prepares view data.
    /// </summary>
    [Authorize]
    public class ReservationsController : ControllerBase<ReservationsController>
    {
        private readonly IReservationService _reservationService;
        private readonly IEquipmentService _equipmentService;

        public ReservationsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IReservationService reservationService,
            IEquipmentService equipmentService)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._reservationService = reservationService;
            this._equipmentService = equipmentService;
        }

        /// <summary>
        /// Borrower equipment availability list (Figma: B - Equipment Availability).
        /// Search matches name, asset code or category; filter narrows to a category.
        /// </summary>
        public IActionResult Available(string search, long? category, int page = 1, int pageSize = 10)
        {
            try
            {
                var model = this._reservationService.GetAvailabilityViewModel(search, category, page, pageSize);

                this.ViewBag.CategoryOptions = this.ToSelectList(
                    this._equipmentService.GetCategoryOptions(), model.CategoryId, "All Categories");

                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Available");
                this.TempData["ErrorMessage"] = "Unable to load the equipment availability list.";
                return this.View(new EquipmentAvailabilityIndexViewModel { Page = new PagedResult<EquipmentAvailabilityRowViewModel>() });
            }
        }

        /// <summary>
        /// Borrower equipment details (Figma: B - Equipment Details, screen 2.2).
        /// </summary>
        [HttpGet]
        public IActionResult EquipmentDetails(long id)
        {
            var model = this._reservationService.GetEquipmentDetailsViewModel(id);
            if (model == null)
            {
                this.TempData["ErrorMessage"] = ErrorMessages.ReservationEquipmentNotFound;
                return this.RedirectToAction(nameof(this.Available));
            }

            return this.View(model);
        }

        /// <summary>
        /// Reservation form (Figma: B - Reservation Form) for one piece of equipment.
        /// </summary>
        [Authorize(Roles = RoleCodes.Borrower)]
        public IActionResult New(long id)
        {
            var context = this._reservationService.GetReservationFormViewModel(id);
            if (context == null)
            {
                this.TempData["ErrorMessage"] = ErrorMessages.ReservationEquipmentNotFound;
                return this.RedirectToAction(nameof(this.Available));
            }

            this.PopulateFormContext(context.Form);
            return this.View(context);
        }

        [Authorize(Roles = RoleCodes.Borrower)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult New(ReservationFormViewModel form)
        {
            // Server-side check for the quantity floor — [Range] is intentionally not used
            // because business limits come from the policy, not from view-annotations.
            if (form.Quantity is null || form.Quantity < 1)
            {
                this.ModelState.AddModelError("Quantity", ErrorMessages.ReservationQuantityInvalid);
            }

            if (!this.ModelState.IsValid)
            {
                this.PopulateFormContext(form);
                return this.View(new ReservationFormContextViewModel { Form = form, Equipment = this.ViewBag.EquipmentSummary as ReservationEquipmentSummaryViewModel });
            }

            var (success, error, _) = this._reservationService.Create(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                this.PopulateFormContext(form);
                return this.View(new ReservationFormContextViewModel { Form = form, Equipment = this.ViewBag.EquipmentSummary as ReservationEquipmentSummaryViewModel });
            }

            this.TempData["SuccessMessage"] = "Reservation request submitted. It will be reviewed by a custodian.";
            return this.RedirectToAction(nameof(this.Mine));
        }

        /// <summary>
        /// The acting borrower's own reservations (Figma: B - My Reservations).
        /// </summary>
        [Authorize(Roles = RoleCodes.Borrower)]
        public IActionResult Mine(string search)
        {
            var model = this._reservationService.GetMineViewModel();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                model.Reservations = model.Reservations
                    .Where(r => (r.ReservationNumber ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (r.EquipmentName ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (r.AssetCode ?? string.Empty).Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }
            this.ViewBag.Search = search;
            return this.View(model);
        }

        /// <summary>
        /// Reservation details (Figma: B - Reservation Details). Borrowers only see their
        /// own reservations; staff may review any.
        /// </summary>
        public IActionResult Details(long id)
        {
            var model = this._reservationService.GetDetailsViewModel(id);
            if (model == null)
            {
                this.TempData["ErrorMessage"] = ErrorMessages.ReservationNotFound;
                return this.RedirectToAction(nameof(this.Mine));
            }

            return this.View(model);
        }

        /// <summary>
        /// Printable pickup slip with a QR of the reservation number; the custodian scans it to
        /// open the release form. Same visibility rules as Details.
        /// </summary>
        public IActionResult Slip(long id)
        {
            var model = this._reservationService.GetDetailsViewModel(id);
            if (model == null)
            {
                this.TempData["ErrorMessage"] = ErrorMessages.ReservationNotFound;
                return this.RedirectToAction(nameof(this.Mine));
            }

            var scanUrl = this.Url.Action("Scan", "Custodian", new { code = model.ReservationNumber }, this.Request.Scheme);
            this.ViewBag.QrSvg = QrCode.ToSvg(scanUrl);
            return this.View(model);
        }

        /// <summary>
        /// Cancels the borrower's own PENDING/APPROVED reservation.
        /// </summary>
        [Authorize(Roles = RoleCodes.Borrower)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Cancel(long id)
        {
            var (success, error) = this._reservationService.Cancel(id);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Reservation cancelled.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }

            return this.RedirectToAction(nameof(this.Mine));
        }

        private void PopulateFormContext(ReservationFormViewModel form)
        {
            var context = this._reservationService.GetReservationFormViewModel(form.EquipmentId)
                ?? new ReservationFormContextViewModel
                {
                    Form = form,
                    Equipment = new ReservationEquipmentSummaryViewModel { EquipmentId = form.EquipmentId },
                };

            this.ViewBag.EquipmentSummary = context.Equipment;
            this.ViewBag.PickupOptions = this.ToSelectList(
                this._reservationService.GetPickupLocationOptions(), form.PickupLocationId, "-- Select pickup point --");
            this.ViewBag.OperatorName = this._reservationService.GetActiveOperatorName();
        }

        private SelectList ToSelectList(
            System.Collections.Generic.IEnumerable<LookupViewModel> options,
            long? selected,
            string emptyText)
        {
            var items = options
                .Select(o => new { Id = o.Id.ToString(), o.Text })
                .ToList();
            var withEmpty = new[] { new { Id = string.Empty, Text = emptyText } }.Concat(items);

            return new SelectList(withEmpty, "Id", "Text", selected?.ToString());
        }
    }
}
