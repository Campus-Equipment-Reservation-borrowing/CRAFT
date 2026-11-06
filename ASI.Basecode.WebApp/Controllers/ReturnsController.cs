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

        public ReturnsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IReturnProcessingService service)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._service = service;
        }

        public IActionResult Index(string search, int page = 1, int pageSize = 10)
        {
            try
            {
                var model = this._service.GetReturnsIndex(search, page, pageSize);
                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Index");
                this.TempData["ErrorMessage"] = "Unable to load returns.";
                return this.View(new ReturnsIndexViewModel { Page = new Data.PagedResult<ReturnRowViewModel>() });
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
            if (!this.ModelState.IsValid)
            {
                var ctx = this._service.GetReturnFormContext(form.BorrowingId);
                this.ViewBag.Context = ctx;
                return this.View(form);
            }

            var (success, error, _) = this._service.CreateReturn(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                var ctx = this._service.GetReturnFormContext(form.BorrowingId);
                this.ViewBag.Context = ctx;
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
            var form = new InspectionFormViewModel
            {
                BorrowingId = id,
                // Inspection follows a return: default to the units already handed back.
                InspectedQuantity = ctx.AlreadyReturned > 0 ? ctx.AlreadyReturned : ctx.ReleasedQuantity,
                GoodQuantity = ctx.AlreadyReturned > 0 ? ctx.AlreadyReturned : ctx.ReleasedQuantity,
                InspectionStatusId = 1,
            };
            this.ViewBag.ConditionOptions = this.ToSelectList(this._service.GetConditionOptions(), null, "-- Select condition --");
            this.ViewBag.InspectionStatusOptions = this.ToSelectList(this._service.GetInspectionStatusOptions(), 1, "-- Select status --");
            return this.View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Inspect(InspectionFormViewModel form, List<IFormFile> photos)
        {
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
            return this.RedirectToAction(nameof(this.Index));
        }
        private SelectList ToSelectList(IEnumerable<LookupViewModel> options, long? selected, string emptyText)
        {
            var items = (options ?? new List<LookupViewModel>()).Select(o => new { Id = o.Id.ToString(), o.Text }).ToList();
            var withEmpty = new[] { new { Id = string.Empty, Text = emptyText } }.Concat(items);
            return new SelectList(withEmpty, "Id", "Text", selected?.ToString());
        }
    }
}
