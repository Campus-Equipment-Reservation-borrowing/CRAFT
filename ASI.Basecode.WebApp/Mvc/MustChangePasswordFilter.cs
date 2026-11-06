using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System;

namespace ASI.Basecode.WebApp.Mvc
{
    /// <summary>
    /// Keeps accounts that still use an admin-issued temporary password on
    /// Account/ChangePassword until they pick their own. The Account controller stays reachable
    /// so the user can change the password or sign out.
    /// </summary>
    public class MustChangePasswordFilter : IActionFilter
    {
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var user = context.HttpContext.User;
            if (user?.Identity?.IsAuthenticated != true
                || user.FindFirst(CampusPulseClaims.MustChangePassword)?.Value != "1")
            {
                return;
            }

            var controller = context.RouteData.Values["controller"]?.ToString();
            if (string.Equals(controller, "Account", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            context.Result = new RedirectToActionResult("ChangePassword", "Account", null);
        }

        public void OnActionExecuted(ActionExecutedContext context)
        {
        }
    }
}
