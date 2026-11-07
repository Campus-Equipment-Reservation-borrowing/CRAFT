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
            if (!this.ModelState.IsValid)
            {
                this.TempData["ErrorMessage"] = "Please enter valid policy values.";
                return this.RedirectToAction(nameof(this.Index));
            }

            var (success, error) = this._service.SavePolicy(form);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Policy version published.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddCategory(string code, string name)
        {
            var (success, error) = this._service.AddCategory(code, name);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Category added.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteCategory(long id)
        {
            var (success, error) = this._service.DeleteCategory(id);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Category deleted.";
            }
            else
            {
                this.TempData["ErrorMessage"] = "Category cannot be deleted (equipment references it).";
            }
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult AddLocation(string code, string name, bool isPickup)
        {
            var (success, error) = this._service.AddLocation(code, name, isPickup);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Location added.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteLocation(long id)
        {
            var (success, error) = this._service.DeleteLocation(id);
            if (success)
            {
                this.TempData["SuccessMessage"] = "Location deleted.";
            }
            else
            {
                this.TempData["ErrorMessage"] = "Location cannot be deleted (equipment references it).";
            }
            return this.RedirectToAction(nameof(this.Index));
        }
    }
}
