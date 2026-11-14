using System;
using System.IO;
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
        private const long MaxPhotoBytes = 2 * 1024 * 1024;
        private const string PhotoWebFolder = "/uploads/profiles/";

        private readonly IProfileService _profileService;
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUser;
        private readonly Microsoft.AspNetCore.Hosting.IWebHostEnvironment _environment;

        public ProfileController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IProfileService profileService,
            IAuthService authService,
            ICurrentUserService currentUser,
            Microsoft.AspNetCore.Hosting.IWebHostEnvironment environment)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._profileService = profileService;
            this._authService = authService;
            this._currentUser = currentUser;
            this._environment = environment;
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

        /// <summary>
        /// Records that the signed-in user finished or skipped the walkthrough (completed=true),
        /// or resets it so the overview plays again (completed=false). Called from guide.js.
        /// </summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult TourCompleted(bool completed = true)
        {
            // The walkthrough belongs to the person signed in, not the operator they act as.
            if (!long.TryParse(this.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                return this.Unauthorized();
            }

            this._profileService.SetTourCompleted(userId, completed);
            return this.NoContent();
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

        /// <summary>Uploads or replaces the signed-in user's profile photo (every role).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(MaxPhotoBytes + 64 * 1024)]
        public IActionResult UploadPhoto(IFormFile photo)
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            var (newPath, error) = this.SaveProfilePhoto(photo);
            if (error != null)
            {
                this.TempData["ErrorMessage"] = error;
                return this.RedirectToAction(nameof(this.Index));
            }

            var (success, saveError, previousPath) = this._profileService.UpdateProfileImage(userId.Value, newPath);
            if (!success)
            {
                this.DeleteProfilePhotoFile(newPath);
                this.TempData["ErrorMessage"] = saveError;
                return this.RedirectToAction(nameof(this.Index));
            }

            this.DeleteProfilePhotoFile(previousPath);
            this.TempData["SuccessMessage"] = "Your profile photo has been updated.";
            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>Removes the signed-in user's profile photo; the avatar falls back to initials.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemovePhoto()
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            var (success, error, previousPath) = this._profileService.UpdateProfileImage(userId.Value, null);
            if (success)
            {
                this.DeleteProfilePhotoFile(previousPath);
            }

            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "Your profile photo has been removed."
                : error;
            return this.RedirectToAction(nameof(this.Index));
        }

        /// <summary>
        /// Validates and stores an uploaded photo. The file type is read from the file's own bytes
        /// (JPG, PNG or WEBP), not from its name, and it is saved under a random name.
        /// </summary>
        private (string Path, string Error) SaveProfilePhoto(IFormFile file)
        {
            if (file == null || file.Length == 0)
            {
                return (null, "Choose a photo to upload.");
            }

            if (file.Length > MaxPhotoBytes)
            {
                return (null, "Photo must be 2 MB or smaller.");
            }

            try
            {
                var header = new byte[12];
                int read;
                using (var probe = file.OpenReadStream())
                {
                    read = probe.Read(header, 0, header.Length);
                }

                var extension = DetectImageExtension(header, read);
                if (extension == null)
                {
                    return (null, "Upload a JPG, PNG or WEBP image.");
                }

                var folder = Path.Combine(this._environment.WebRootPath, "uploads", "profiles");
                Directory.CreateDirectory(folder);

                var fileName = Guid.NewGuid().ToString("N") + extension;
                using (var stream = new FileStream(Path.Combine(folder, fileName), FileMode.CreateNew))
                {
                    file.CopyTo(stream);
                }

                return (PhotoWebFolder + fileName, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save profile photo.");
                return (null, "Unable to save the photo. Please try again.");
            }
        }

        private static string DetectImageExtension(byte[] h, int length)
        {
            if (length >= 3 && h[0] == 0xFF && h[1] == 0xD8 && h[2] == 0xFF)
            {
                return ".jpg";
            }

            if (length >= 8 && h[0] == 0x89 && h[1] == 0x50 && h[2] == 0x4E && h[3] == 0x47
                && h[4] == 0x0D && h[5] == 0x0A && h[6] == 0x1A && h[7] == 0x0A)
            {
                return ".png";
            }

            if (length >= 12 && h[0] == (byte)'R' && h[1] == (byte)'I' && h[2] == (byte)'F' && h[3] == (byte)'F'
                && h[8] == (byte)'W' && h[9] == (byte)'E' && h[10] == (byte)'B' && h[11] == (byte)'P')
            {
                return ".webp";
            }

            return null;
        }

        /// <summary>Deletes a stored photo file; ignores anything outside the profiles upload folder.</summary>
        private void DeleteProfilePhotoFile(string webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath) || !webPath.StartsWith(PhotoWebFolder, StringComparison.Ordinal))
            {
                return;
            }

            try
            {
                var fileName = Path.GetFileName(webPath);
                var absolutePath = Path.Combine(this._environment.WebRootPath, "uploads", "profiles", fileName);
                if (System.IO.File.Exists(absolutePath))
                {
                    System.IO.File.Delete(absolutePath);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Could not delete old profile photo {Path}.", webPath);
            }
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
