using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Resolves the acting user for audit logging from the authenticated cookie principal only.
    /// (The legacy session-based operator switcher has been retired.)
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public long? GetActorUserId()
        {
            var httpContext = this._httpContextAccessor.HttpContext;

            var raw = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return long.TryParse(raw, out var id) ? id : null;
        }

        public string GetActorName()
        {
            var httpContext = this._httpContextAccessor.HttpContext;

            return httpContext?.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.Identity.Name
                : null;
        }

        public string GetActorRoleCode()
        {
            return this._httpContextAccessor.HttpContext?.User?.FindFirst(ClaimTypes.Role)?.Value;
        }

        public bool IsActorAdmin()
        {
            return string.Equals(this.GetActorRoleCode(), ASI.Basecode.Resources.Constants.RoleCodes.Admin, StringComparison.OrdinalIgnoreCase);
        }
    }
}
