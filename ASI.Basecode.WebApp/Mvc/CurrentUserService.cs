using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Security.Claims;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Resolves the acting user for audit logging.
    /// Primary source is the authenticated cookie principal; the session keys remain as a
    /// fallback for the legacy operator switcher.
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private const string ActorUserIdSessionKey = "CurrentUserId";
        private const string ActorNameSessionKey = "CurrentUserName";

        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            this._httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        public long? GetActorUserId()
        {
            var httpContext = this._httpContextAccessor.HttpContext;

            var raw = httpContext?.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(raw, out var id))
            {
                return id;
            }

            var sessionRaw = httpContext?.Session?.GetString(ActorUserIdSessionKey);
            return long.TryParse(sessionRaw, out var sessionId) ? sessionId : null;
        }

        public string GetActorName()
        {
            var httpContext = this._httpContextAccessor.HttpContext;

            var name = httpContext?.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.Identity.Name
                : null;

            return string.IsNullOrWhiteSpace(name)
                ? httpContext?.Session?.GetString(ActorNameSessionKey)
                : name;
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
