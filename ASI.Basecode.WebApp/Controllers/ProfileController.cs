using System.Linq;
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
    /// Profile &amp; Settings: 2.10 (borrower), 3.10 (custodian), 4.8 (administrator).
    /// The controller picks the role-specific view but shares one service.
    /// </summary>
    [Authorize]
    public class ProfileController : ControllerBase<ProfileController>
    {
        private readonly IProfileService _profileService;
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUser;

        public ProfileController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IProfileService profileService,
            IAuthService authService,
            ICurrentUserService currentUser)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._profileService = profileService;
            this._authService = authService;
            this._currentUser = currentUser;
        }

        [HttpGet]
        public IActionResult Index()
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            var model = this._profileService.GetProfile(userId.Value);
            if (model == null)
            {
                return this.RedirectToAction("Login", "Account");
            }

            return this.View(this.ResolveView(model.RoleCode), model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SaveContact(ProfileContactFormViewModel form)
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            if (!this.ModelState.IsValid)
            {
                var model = this._profileService.GetProfile(userId.Value);
                this.TempData["ErrorMessage"] = "Please correct the highlighted fields.";
                return this.View(this.ResolveView(model?.RoleCode), model);
            }

            var (success, error) = this._profileService.SaveContact(userId.Value, form);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "Your changes have been saved."
                : error;

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ChangePassword(ProfilePasswordFormViewModel form)
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            if (!this.ModelState.IsValid)
            {
                // Show the first real reason (e.g. "Passwords do not match.") rather than a generic one.
                var first = this.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage).FirstOrDefault(m => !string.IsNullOrWhiteSpace(m));
                this.TempData["ErrorMessage"] = first ?? "Please complete all required fields.";
                return this.RedirectToAction(nameof(this.Index));
            }

            var (success, error) = this._authService.ChangePassword(userId.Value, form.CurrentPassword, form.NewPassword);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "Your password has been updated."
                : error;

            return this.RedirectToAction(nameof(this.Index));
        }

        private string ResolveView(string roleCode)
        {
            if (string.Equals(roleCode, RoleCodes.Technician, System.StringComparison.OrdinalIgnoreCase))
            {
                return "Custodian";
            }

            if (string.Equals(roleCode, RoleCodes.Admin, System.StringComparison.OrdinalIgnoreCase))
            {
                return "Admin";
            }

            return "Borrower";
        }
    }
}
