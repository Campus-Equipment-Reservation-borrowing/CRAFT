using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Claims;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Builds the auth cookie principal and keeps signed-in sessions in step with the database:
    /// suspended / deactivated / locked accounts, role changes and password resets end the session,
    /// and a changed account status (e.g. just verified) is refreshed in place.
    /// </summary>
    public static class AuthCookie
    {
        private const string LastCheckedKey = "cp:last_checked";
        private static readonly TimeSpan CheckInterval = TimeSpan.FromMinutes(1);

        public static ClaimsPrincipal BuildPrincipal(AuthenticatedUserViewModel user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString(CultureInfo.InvariantCulture)),
                new Claim(ClaimTypes.Name, user.FullName ?? user.SchoolEmail ?? string.Empty),
                new Claim(ClaimTypes.Email, user.SchoolEmail ?? string.Empty),
                new Claim(ClaimTypes.Role, user.RoleCode ?? string.Empty),
                new Claim(CampusPulseClaims.AccountStatus, user.AccountStatusCode ?? string.Empty),
                new Claim(CampusPulseClaims.AccountIdentifier, user.AccountIdentifier ?? string.Empty),
                new Claim(CampusPulseClaims.IsBorrower, user.IsBorrowerRole ? "1" : "0"),
                new Claim(CampusPulseClaims.Department, user.DepartmentName ?? string.Empty),
                new Claim(CampusPulseClaims.PasswordStamp, user.PasswordStamp ?? string.Empty),
            };

            if (user.MustChangePassword)
            {
                claims.Add(new Claim(CampusPulseClaims.MustChangePassword, "1"));
            }

            return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
        }

        /// <summary>Cookie <c>OnValidatePrincipal</c> handler; hits the database at most once a minute per session.</summary>
        public static async Task ValidateAsync(CookieValidatePrincipalContext context)
        {
            var now = DateTimeOffset.UtcNow;
            if (context.Properties.Items.TryGetValue(LastCheckedKey, out var raw)
                && DateTimeOffset.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var lastChecked)
                && now - lastChecked < CheckInterval)
            {
                return;
            }

            var principal = context.Principal;
            if (!long.TryParse(principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var userId))
            {
                await Reject(context);
                return;
            }

            var authService = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
            var current = authService.GetAuthenticatedUser(userId);
            var status = current?.AccountStatusCode;
            var disabled = status == AccountStatusCodes.Suspended
                || status == AccountStatusCodes.Inactive
                || status == AccountStatusCodes.Locked;

            if (current == null
                || disabled
                || !string.Equals(current.RoleCode, principal.FindFirst(ClaimTypes.Role)?.Value, StringComparison.Ordinal)
                || !string.Equals(current.PasswordStamp, principal.FindFirst(CampusPulseClaims.PasswordStamp)?.Value, StringComparison.Ordinal))
            {
                await Reject(context);
                return;
            }

            context.Properties.Items[LastCheckedKey] = now.ToString("o", CultureInfo.InvariantCulture);
            if (!string.Equals(status, principal.FindFirst(CampusPulseClaims.AccountStatus)?.Value, StringComparison.Ordinal)
                || current.MustChangePassword != (principal.FindFirst(CampusPulseClaims.MustChangePassword)?.Value == "1"))
            {
                context.ReplacePrincipal(BuildPrincipal(current));
            }

            context.ShouldRenew = true;
        }

        private static async Task Reject(CookieValidatePrincipalContext context)
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
