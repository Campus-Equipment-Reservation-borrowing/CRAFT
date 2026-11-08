using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.WebApp.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.ViewComponents
{
    public class ProfileAvatarViewComponent : ViewComponent
    {
        private readonly IProfileService _profileService;
        private readonly ILogger<ProfileAvatarViewComponent> _logger;

        public ProfileAvatarViewComponent(IProfileService profileService, ILogger<ProfileAvatarViewComponent> logger)
        {
            this._profileService = profileService;
            this._logger = logger;
        }

        public IViewComponentResult Invoke(string displayName)
        {
            var initials = string.IsNullOrWhiteSpace(displayName)
                ? "U"
                : displayName.Trim().Substring(0, 1).ToUpperInvariant();
            string imagePath = null;

            if (long.TryParse(this.UserClaimsPrincipal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                try
                {
                    imagePath = this._profileService.GetProfileImagePath(userId);
                }
                catch (Exception ex)
                {
                    this._logger.LogWarning(ex, "Unable to read profile photo for user {UserId}.", userId);
                }
            }

            return this.View(new ProfileAvatarViewModel
            {
                ImagePath = imagePath,
                Initials = initials,
            });
        }
    }
}
