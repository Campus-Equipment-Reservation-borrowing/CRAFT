using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.IO;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Profile &amp; Settings: 2.10 (borrower), 3.10 (custodian), 4.8 (administrator).
    /// The controller picks the role-specific view but shares one service.
    /// </summary>
    [Authorize]
    public class ProfileController : ControllerBase<ProfileController>
    {
        private const long MaxProfilePhotoBytes = 5 * 1024 * 1024;
        private static readonly string[] AllowedProfilePhotoExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private readonly IProfileService _profileService;
        private readonly IAuthService _authService;
        private readonly ICurrentUserService _currentUser;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            IProfileService profileService,
            IAuthService authService,
            ICurrentUserService currentUser,
            IWebHostEnvironment environment)
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
                if (model == null)
                {
                    return this.RedirectToAction("Login", "Account");
                }

                this.TempData["ErrorMessage"] = "Please correct the highlighted fields.";
                return this.View(this.ResolveView(model.RoleCode), model);
            }

            var (success, error) = this._profileService.SaveContact(userId.Value, form);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success
                ? "Your changes have been saved."
                : error;

            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult RemovePhoto()
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            var (success, error, previousImagePath) = this._profileService.SaveProfileImage(userId.Value, null);
            if (!success)
            {
                this.TempData["ErrorMessage"] = error;
                return this.RedirectToAction(nameof(this.Index));
            }

            this.DeletePreviousProfilePhoto(previousImagePath);
            this.TempData["SuccessMessage"] = "Your profile photo has been removed.";
            return this.RedirectToAction(nameof(this.Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UploadPhoto(IFormFile profilePhoto)
        {
            var userId = this._currentUser.GetActorUserId();
            if (!userId.HasValue)
            {
                return this.RedirectToAction("Login", "Account");
            }

            if (profilePhoto == null || profilePhoto.Length == 0)
            {
                this.TempData["ErrorMessage"] = "Choose a photo to upload.";
                return this.RedirectToAction(nameof(this.Index));
            }

            var extension = Path.GetExtension(profilePhoto.FileName)?.ToLowerInvariant();
            if (profilePhoto.Length > MaxProfilePhotoBytes)
            {
                this.TempData["ErrorMessage"] = "Profile photos must be 5 MB or smaller.";
                return this.RedirectToAction(nameof(this.Index));
            }

            if (string.IsNullOrWhiteSpace(extension)
                || !Array.Exists(AllowedProfilePhotoExtensions, allowed => allowed == extension)
                || !HasValidImageSignature(profilePhoto, extension))
            {
                this.TempData["ErrorMessage"] = "Upload a valid JPG, PNG, WEBP or GIF photo.";
                return this.RedirectToAction(nameof(this.Index));
            }

            var fileName = Guid.NewGuid().ToString("N") + extension;
            var uploadDirectory = Path.Combine(this._environment.WebRootPath, "uploads", "profiles");
            var destinationPath = Path.Combine(uploadDirectory, fileName);
            var imagePath = "/uploads/profiles/" + fileName;

            try
            {
                Directory.CreateDirectory(uploadDirectory);
                using (var stream = new FileStream(destinationPath, FileMode.CreateNew, FileAccess.Write))
                {
                    profilePhoto.CopyTo(stream);
                }

                var (success, error, previousImagePath) = this._profileService.SaveProfileImage(userId.Value, imagePath);
                if (!success)
                {
                    System.IO.File.Delete(destinationPath);
                    this.TempData["ErrorMessage"] = error;
                    return this.RedirectToAction(nameof(this.Index));
                }

                this.DeletePreviousProfilePhoto(previousImagePath);
                this.TempData["SuccessMessage"] = "Your profile photo has been updated.";
            }
            catch (Exception ex)
            {
                if (System.IO.File.Exists(destinationPath))
                {
                    System.IO.File.Delete(destinationPath);
                }

                this._logger.LogError(ex, "Failed to upload profile photo for user {UserId}.", userId.Value);
                this.TempData["ErrorMessage"] = "Unable to upload the profile photo. Please try again.";
            }

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
                this.TempData["ErrorMessage"] = "Please complete all required fields.";
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

        private static bool HasValidImageSignature(IFormFile file, string extension)
        {
            var header = new byte[12];
            using (var stream = file.OpenReadStream())
            {
                var bytesRead = stream.Read(header, 0, header.Length);
                if (extension is ".jpg" or ".jpeg")
                {
                    return bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                }

                if (extension == ".png")
                {
                    return bytesRead >= 8
                        && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
                        && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
                }

                if (extension == ".webp")
                {
                    return bytesRead >= 12
                        && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
                        && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';
                }

                return extension == ".gif" && bytesRead >= 6
                    && header[0] == (byte)'G' && header[1] == (byte)'I' && header[2] == (byte)'F'
                    && header[3] == (byte)'8' && (header[4] == (byte)'7' || header[4] == (byte)'9') && header[5] == (byte)'a';
            }
        }

        private void DeletePreviousProfilePhoto(string imagePath)
        {
            const string uploadPrefix = "/uploads/profiles/";
            if (string.IsNullOrWhiteSpace(imagePath) || !imagePath.StartsWith(uploadPrefix, StringComparison.Ordinal))
            {
                return;
            }

            var fileName = imagePath.Substring(uploadPrefix.Length);
            if (string.IsNullOrWhiteSpace(fileName) || Path.GetFileName(fileName) != fileName)
            {
                return;
            }

            var previousPath = Path.Combine(this._environment.WebRootPath, "uploads", "profiles", fileName);
            try
            {
                if (System.IO.File.Exists(previousPath))
                {
                    System.IO.File.Delete(previousPath);
                }
            }
            catch (Exception ex)
            {
                this._logger.LogWarning(ex, "Could not remove previous profile photo {ImagePath}.", imagePath);
            }
        }
    }
}
