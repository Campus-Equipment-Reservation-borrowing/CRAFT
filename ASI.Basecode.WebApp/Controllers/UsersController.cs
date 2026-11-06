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
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// User Management screens (Figma: A - User Management / A - User Details).
    /// Thin controller: binds requests, delegates to IUserService, prepares view data.
    /// </summary>
    [Authorize(Roles = RoleCodes.Admin)]
    public class UsersController : ControllerBase<UsersController>
    {
        private readonly IUserService _userService;
        private readonly IAuthService _authService;
        private readonly ICustodianAssignmentService _assignments;
        private readonly ICommunicationService _communication;

        public UsersController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IUserService userService,
            IAuthService authService,
            ICustodianAssignmentService assignments,
            ICommunicationService communication)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._userService = userService;
            this._authService = authService;
            this._assignments = assignments;
            this._communication = communication;
        }

        /// <summary>
        /// User list with search, codebook filters and paging (10/25 per page).
        /// </summary>
        public IActionResult Index(
            string search,
            long? role,
            long? department,
            long? status,
            int page = 1,
            int pageSize = 10)
        {
            try
            {
                var model = this._userService.GetIndexViewModel(
                    search, role, department, status, page, pageSize);

                this.ViewBag.RoleOptions = this.ToSelectList(
                    this._userService.GetRoleOptions(), model.RoleId, "All Roles");
                this.ViewBag.DepartmentOptions = this.ToSelectList(
                    this._userService.GetDepartmentOptions(), model.DepartmentId, "All Departments");
                this.ViewBag.StatusOptions = this.ToSelectList(
                    this._userService.GetAccountStatusOptions(), model.AccountStatusId, "All Statuses");
                this.ViewBag.PageSizeOptions = new SelectList(
                    new[] { new { Id = "10", Text = "10 per page" }, new { Id = "25", Text = "25 per page" }, },
                    "Id", "Text", model.PageSize.ToString());

                return this.View(model);
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Index");
                this.TempData["ErrorMessage"] = "Unable to load the user list.";
                return this.View(new UserIndexViewModel { Page = new PagedResult<UserListViewModel>() });
            }
        }

        public IActionResult Create()
        {
            this.ViewBag.RoleOptions = this.ToSelectList(this._userService.GetRoleOptions(), null, "-- Select role --");
            this.ViewBag.DepartmentOptions = this.ToSelectList(this._userService.GetDepartmentOptions(), null, "-- None --");
            this.ViewBag.StatusOptions = this.ToSelectList(this._userService.GetAccountStatusOptions(), null, "-- Select status --");

            return this.View(new UserFormViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(UserFormViewModel form)
        {
            if (!this.ModelState.IsValid)
            {
                this.PopulateFormSelectLists(form.RoleId, form.DepartmentId, form.AccountStatusId);
                return this.View(form);
            }

            var (success, error, userId) = this._userService.Save(form);
            if (!success)
            {
                form.TemporaryPassword = null;
                this.ModelState.AddModelError(string.Empty, error);
                this.PopulateFormSelectLists(form.RoleId, form.DepartmentId, form.AccountStatusId);
                return this.View(form);
            }

            // Shown once on the details page; TempData is consumed on read.
            this.TempData["IssuedPassword"] = form.IssuedTemporaryPassword;
            this.TempData["SuccessMessage"] = "User account created.";
            return this.RedirectToAction(nameof(this.Details), new { id = userId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult ResetPassword(long id)
        {
            var (success, error, temporary) = this._userService.ResetPassword(id);
            if (success)
            {
                this.TempData["IssuedPassword"] = temporary;
                this.TempData["SuccessMessage"] = "Temporary password issued.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        /// <summary>
        /// One-time set-password link (reuses the password reset token flow); there is no mail
        /// transport yet, so the link is shown to the admin to hand over.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult InviteLink(long id)
        {
            var user = this._userService.GetByIdWithDetails(id);
            if (user == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.UserNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            var (success, error, token) = this._authService.CreatePasswordResetToken(user.SchoolEmail);
            if (success)
            {
                this.TempData["InviteLink"] = this.Url.Action("ResetPassword", "Account", new { token }, this.Request.Scheme);
                this.TempData["SuccessMessage"] = "Invite link created.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }

            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        public IActionResult Edit(long id)
        {
            var form = this._userService.GetForEdit(id);
            if (form == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.UserNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            this.PopulateFormSelectLists(form.RoleId, form.DepartmentId, form.AccountStatusId);
            return this.View(form);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Edit(long id, UserFormViewModel form)
        {
            form.UserId = id;

            if (!this.ModelState.IsValid)
            {
                this.PopulateFormSelectLists(form.RoleId, form.DepartmentId, form.AccountStatusId);
                return this.View(form);
            }

            var (success, error, _) = this._userService.Save(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                this.PopulateFormSelectLists(form.RoleId, form.DepartmentId, form.AccountStatusId);
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "User account updated.";
            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>
        /// Read-only user profile detail view (Figma: A - User Details).
        /// </summary>
        public IActionResult Details(long id)
        {
            var entity = this._userService.GetByIdWithDetails(id);
            if (entity == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.UserNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            this.ViewBag.AssignmentSummary = this._assignments.GetUserSummary(id);
            return this.View(entity);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Delete(long id)
        {
            var (success, error) = this._userService.Delete(id);
            if (success)
            {
                this.TempData["SuccessMessage"] = "User account deleted.";
            }
            else
            {
                this.TempData["ErrorMessage"] = error;
            }

            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>
        /// One borrower's reservation and borrowing history (per-user drill-down).
        /// </summary>
        public IActionResult History(long id)
        {
            var model = this._communication.GetBorrowerHistory(id);
            if (model == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.UserNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(model);
        }

        /// <summary>
        /// Pending Verifications queue (UNVERIFIED accounts) with a Rejected tab.
        /// </summary>
        public IActionResult Verifications(string tab = "pending", string search = null, int page = 1)
        {
            try
            {
                return this.View(this._userService.GetVerificationQueue(tab, search, page, 10));
            }
            catch (Exception ex)
            {
                this.HandleExceptionLog(ex, "Verifications");
                this.TempData["ErrorMessage"] = "Unable to load the verification queue.";
                return this.View(new VerificationQueueViewModel { Page = new PagedResult<UserListViewModel>(), Tab = "pending" });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Verify(long id, string returnTab = "pending")
        {
            var (success, error) = this._userService.Verify(id);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Account verified." : error;
            return this.RedirectToAction(nameof(this.Verifications), new { tab = returnTab });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RejectVerification(long id, string reason)
        {
            var (success, error) = this._userService.RejectVerification(id, reason);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Verification rejected." : error;
            return this.RedirectToAction(nameof(this.Verifications));
        }

        private void PopulateFormSelectLists(long? roleId, long? departmentId, long? accountStatusId)
        {
            this.ViewBag.RoleOptions = this.ToSelectList(this._userService.GetRoleOptions(), roleId, "-- Select role --");
            this.ViewBag.DepartmentOptions = this.ToSelectList(this._userService.GetDepartmentOptions(), departmentId, "-- None --");
            this.ViewBag.StatusOptions = this.ToSelectList(this._userService.GetAccountStatusOptions(), accountStatusId, "-- Select status --");
        }

        private SelectList ToSelectList(IEnumerable<LookupViewModel> options, long? selected, string emptyText)
        {
            var items = options
                .Select(o => new { Id = o.Id.ToString(), o.Text })
                .ToList();

            // emptyText row first
            var withEmpty = new[] { new { Id = string.Empty, Text = emptyText } }.Concat(items);

            return new SelectList(withEmpty, "Id", "Text", selected?.ToString());
        }
    }
}
