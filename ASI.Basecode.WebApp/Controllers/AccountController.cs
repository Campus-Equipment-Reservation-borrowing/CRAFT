using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using AuthSignInResult = ASI.Basecode.Services.Interfaces.SignInResult;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Authentication and account self-service: screens 1.2, 1.1, 1.3 and 1.4.
    /// </summary>
    public class AccountController : ControllerBase<AccountController>
    {
        private readonly IAuthService _authService;

        public AccountController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IAuthService authService)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._authService = authService;
        }

        [HttpGet]
        public IActionResult Login(string returnUrl = null)
        {
            if (this.User?.Identity?.IsAuthenticated == true)
            {
                return this.RedirectToAction("Index", "Home");
            }

            return this.View(new LoginViewModel { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.UserId) || string.IsNullOrEmpty(model.Password))
            {
                this.ModelState.AddModelError(string.Empty, "Please enter your username and password.");
                return this.View(model);
            }

            var (result, user, error) = this._authService.SignIn(model.UserId, model.Password);
            if (result != AuthSignInResult.Success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(model);
            }

            await this.SignInAsync(user, model.RememberMe);

            if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && this.Url.IsLocalUrl(model.ReturnUrl))
            {
                return this.Redirect(model.ReturnUrl);
            }

            return this.RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult Register()
        {
            var model = new RegisterViewModel
            {
                RoleOptions = this._authService.GetBorrowerRoleOptions(),
                DepartmentOptions = this._authService.GetDepartmentOptions(),
            };

            return this.View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Register(RegisterViewModel model)
        {
            model.RoleOptions = this._authService.GetBorrowerRoleOptions();
            model.DepartmentOptions = this._authService.GetDepartmentOptions();

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var (success, error, _) = this._authService.Register(model);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(model);
            }

            this.TempData["AuthMessage"] = "Registration successful. Your account is pending verification.";
            return this.RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult ForgotPassword()
        {
            return this.View(new ForgotPasswordViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ForgotPassword(ForgotPasswordViewModel model)
        {
            if (string.IsNullOrWhiteSpace(model.Email))
            {
                this.ModelState.AddModelError(string.Empty, "Please enter your registered email address.");
                return this.View(model);
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var (success, error, token) = this._authService.CreatePasswordResetToken(model.Email);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(model);
            }

            model.LinkSent = true;
            model.GeneratedResetUrl = this.Url.Action(
                nameof(ResetPassword), "Account",
                new { token },
                this.Request.Scheme);

            return this.View(model);
        }

        [HttpGet]
        public IActionResult ResetPassword(string token)
        {
            if (!this._authService.IsResetTokenValid(token))
            {
                this.TempData["AuthError"] = "This reset link is no longer valid. Please request a new one.";
                return this.RedirectToAction(nameof(ForgotPassword));
            }

            return this.View(new ResetPasswordViewModel { Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(ResetPasswordViewModel model)
        {
            if (!this._authService.IsResetTokenValid(model.Token))
            {
                this.TempData["AuthError"] = "This reset link is no longer valid. Please request a new one.";
                return this.RedirectToAction(nameof(ForgotPassword));
            }

            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var (success, error) = this._authService.ResetPassword(model);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(model);
            }

            this.TempData["AuthMessage"] = "Your password has been updated.";
            return this.RedirectToAction(nameof(Login));
        }

        /// <summary>
        /// Forced first-sign-in password change for admin-provisioned / admin-reset accounts.
        /// </summary>
        [HttpGet]
        [Authorize]
        public IActionResult ChangePassword()
        {
            return this.View(new ForcedPasswordChangeViewModel());
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ForcedPasswordChangeViewModel model)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(model);
            }

            var userId = long.TryParse(this.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : 0;
            var (success, error) = this._authService.ChangePassword(userId, model.CurrentPassword, model.NewPassword);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(model);
            }

            // Re-issue the cookie without the must-change claim.
            var refreshed = this._authService.GetAuthenticatedUser(userId);
            if (refreshed != null)
            {
                await this.SignInAsync(refreshed, persistent: false);
            }

            this.TempData["SuccessMessage"] = "Password updated. Welcome to CRAFT.";
            return this.RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            if (long.TryParse(this.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var signedInUserId))
            {
                this._authService.RecordSignOut(signedInUserId);
            }

            await this.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // A password change or sign out ends the legacy operator session too.
            this._httpContextAccessor.HttpContext?.Session?.Clear();

            return this.RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            this.Response.StatusCode = StatusCodes.Status403Forbidden;
            return this.View();
        }

        private async Task SignInAsync(AuthenticatedUserViewModel user, bool persistent)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName ?? user.SchoolEmail),
                new Claim(ClaimTypes.Email, user.SchoolEmail ?? string.Empty),
                new Claim(ClaimTypes.Role, user.RoleCode ?? string.Empty),
                new Claim(CampusPulseClaims.AccountStatus, user.AccountStatusCode ?? string.Empty),
                new Claim(CampusPulseClaims.AccountIdentifier, user.AccountIdentifier ?? string.Empty),
                new Claim(CampusPulseClaims.IsBorrower, (user.IsBorrowerRole ? "1" : "0")),
                new Claim(CampusPulseClaims.Department, user.DepartmentName ?? string.Empty),
            };

            if (user.MustChangePassword)
            {
                claims.Add(new Claim(CampusPulseClaims.MustChangePassword, "1"));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            await this.HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties
                {
                    IsPersistent = persistent,
                    ExpiresUtc = persistent
                        ? DateTimeOffset.UtcNow.AddDays(14)
                        : DateTimeOffset.UtcNow.AddMinutes(30),
                });
        }

    }
}
