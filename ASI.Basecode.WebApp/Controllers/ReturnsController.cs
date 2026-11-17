using ASI.Basecode.Data.QueryModels;
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
    public class ReturnsController : ControllerBase<ReturnsController>
    {
        private readonly IReturnProcessingService _service;
        private readonly ICustodianService _custodianService;

        public ReturnsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IReturnProcessingService service,
            ICustodianService custodianService)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._service = service;
            this._custodianService = custodianService;
        }

        public IActionResult Index(string search, int page = 1, int pageSize = 10)
        {
            try
            {
                // Open loans scoped to the assigned custodian, so a return can start without a code/QR.
                // ponytail: first 50 open loans; add separate paging if a custodian ever has more
                this.ViewBag.Borrowed = this._custodianService.GetActiveBorrowings(search, 1, 50);
                var model = this._service.GetReturnsIndex(search, page, pageSize);
                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Index");
                this.TempData["ErrorMessage"] = "Unable to load returns.";
                this.ViewBag.Borrowed = new ActiveBorrowingsIndexViewModel { Page = new PagedResult<ActiveBorrowingRowViewModel>() };
                return this.View(new ReturnsIndexViewModel { Page = new PagedResult<ReturnRowViewModel>() });
            }
        }

        public IActionResult Create(long id)
        {
            var ctx = this._service.GetReturnFormContext(id);
            if (ctx == null)
            {
                this.TempData["ErrorMessage"] = "Borrowing not found.";
                return this.RedirectToAction(nameof(this.Index));
            }
            this.ViewBag.Context = ctx;
            return this.View(ctx.Form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(ReturnFormViewModel form)
        {
            var ctx = this._service.GetReturnFormContext(form?.BorrowingId ?? 0);
            if (ctx == null)
            {
                this.TempData["ErrorMessage"] = "Borrowing not found.";
                return this.RedirectToAction(nameof(this.Index));
            }

            if (!this.ModelState.IsValid)
            {
                this.ViewBag.Context = ctx;
                return this.View(form);
            }

            var (success, error, _) = this._service.CreateReturn(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                this.ViewBag.Context = this._service.GetReturnFormContext(form.BorrowingId) ?? ctx;
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "Return recorded.";
            return this.RedirectToAction(nameof(this.Index));
        }

        public IActionResult Inspect(long id)
        {
            var ctx = this._service.GetReturnFormContext(id);
            if (ctx == null)
            {
                this.TempData["ErrorMessage"] = "Borrowing not found.";
                return this.RedirectToAction(nameof(this.Index));
            }
            this.ViewBag.Context = ctx;
            this.ViewBag.Photos = this._service.GetInspectionPhotos(id);
            var form = new InspectionFormViewModel
            {
                BorrowingId = id,
                // Default to the returned units that haven't been through a completed inspection yet.
                InspectedQuantity = ctx.AwaitingInspection,
                GoodQuantity = ctx.AwaitingInspection,
                InspectionStatusId = ctx.PendingInspectionStatusId,
            };
            if (ctx.AwaitingInspection < 1)
            {
                this.ViewBag.InfoMessage = "No returned units are waiting for inspection. Record a return first.";
            }
            this.ViewBag.ConditionOptions = this.ToSelectList(this._service.GetConditionOptions(), null, "-- Select condition --");
            this.ViewBag.InspectionStatusOptions = this.ToSelectList(this._service.GetInspectionStatusOptions(), ctx.PendingInspectionStatusId, "-- Select status --");
            return this.View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Inspect(InspectionFormViewModel form, List<IFormFile> photos)
        {
            if (this._service.GetReturnFormContext(form?.BorrowingId ?? 0) == null)
            {
                this.TempData["ErrorMessage"] = "Borrowing not found.";
                return this.RedirectToAction(nameof(this.Index));
            }

            if (!this.ModelState.IsValid)
            {
                var ctx = this._service.GetReturnFormContext(form.BorrowingId);
                this.ViewBag.Context = ctx;
                this.ViewBag.ConditionOptions = this.ToSelectList(this._service.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
                this.ViewBag.InspectionStatusOptions = this.ToSelectList(this._service.GetInspectionStatusOptions(), form.InspectionStatusId, "-- Select status --");
                return this.View(form);
            }

            var (success, error, _) = this._service.CreateInspection(form, photos);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                var ctx = this._service.GetReturnFormContext(form.BorrowingId);
                this.ViewBag.Context = ctx;
                this.ViewBag.ConditionOptions = this.ToSelectList(this._service.GetConditionOptions(), form.CurrentConditionId, "-- Select condition --");
                this.ViewBag.InspectionStatusOptions = this.ToSelectList(this._service.GetInspectionStatusOptions(), form.InspectionStatusId, "-- Select status --");
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "Condition inspection recorded.";
            if (!string.IsNullOrEmpty(form.PhotoWarning))
            {
                this.TempData["ErrorMessage"] = form.PhotoWarning;
            }
            return this.RedirectToAction(nameof(this.Index));
        }
        /// <summary>Serves an inspection photo to staff in scope; photos are not public files.</summary>
        [HttpGet]
        public IActionResult Photo(long id, string file)
        {
            var (path, contentType) = this._service.GetInspectionPhotoFile(id, file);
            if (path == null)
            {
                return this.NotFound();
            }

            // Private file: never reuse a cached copy for another signed-in user.
            this.Response.Headers.CacheControl = "private, no-store";
            return this.PhysicalFile(path, contentType);
        }

        private SelectList ToSelectList(IEnumerable<LookupViewModel> options, long? selected, string emptyText)
        {
            var items = (options ?? new List<LookupViewModel>()).Select(o => new { Id = o.Id.ToString(), o.Text }).ToList();
            var withEmpty = new[] { new { Id = string.Empty, Text = emptyText } }.Concat(items);
            return new SelectList(withEmpty, "Id", "Text", selected?.ToString());
        }
    }
}
