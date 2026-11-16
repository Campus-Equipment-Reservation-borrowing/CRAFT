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
    [Authorize(Roles = RoleCodes.Admin)]
    public class SettingsController : ControllerBase<SettingsController>
    {
        private readonly ISettingsService _service;

        public SettingsController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ISettingsService service)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._service = service;
        }

        public IActionResult Index()
        {
            return this.View(this._service.GetSettings());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SavePolicy([Bind(Prefix = "Policy")] PolicyFormViewModel form)
        {
            // Inputs are rendered as Policy.* (bound to SettingsIndexViewModel.Policy), hence the prefix.
            if (!this.ModelState.IsValid)
            {
                var first = this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault();
                this.TempData["ErrorMessage"] = string.IsNullOrWhiteSpace(first) ? "Some policy values are out of range." : first;
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.Result(this._service.SavePolicy(form), "#policy");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCategory(string code, string name)
        {
            return this.Result(this._service.AddCategory(code, name), "#categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(long id)
        {
            return this.Result(this._service.DeleteCategory(id), "#categories");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddLocation(string code, string name, bool isPickup)
        {
            return this.Result(this._service.AddLocation(code, name, isPickup), "#locations");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteLocation(long id)
        {
            return this.Result(this._service.DeleteLocation(id), "#locations");
        }

        /// <summary>The service returns its user-facing message in Error for both outcomes.</summary>
        private IActionResult Result((bool Success, string Error) outcome, string anchor)
        {
            this.TempData[outcome.Success ? "SuccessMessage" : "ErrorMessage"] = outcome.Error;
            return this.Redirect(this.Url.Action(nameof(this.Index)) + anchor);
        }
    }
}
