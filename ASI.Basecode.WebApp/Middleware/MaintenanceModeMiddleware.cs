using ASI.Basecode.Resources.Constants;
using ASI.Basecode.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System;
using System.Threading.Tasks;

namespace ASI.Basecode.WebApp.Middleware
{
    /// <summary>
    /// When an active MAINTENANCE announcement exists, everyone except administrators is sent to
    /// the maintenance page. Sign-in stays available so an administrator can unlock the system.
    /// </summary>
    public class MaintenanceModeMiddleware
    {
        private readonly RequestDelegate _next;

        public MaintenanceModeMiddleware(RequestDelegate next)
        {
            this._next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICommunicationService communication)
        {
            if (this.IsAlwaysAllowed(context.Request.Path))
            {
                await this._next(context);
                return;
            }

            if (context.User?.Identity?.IsAuthenticated == true && context.User.IsInRole(RoleCodes.Admin))
            {
                await this._next(context);
                return;
            }

            bool underMaintenance;
            try
            {
                underMaintenance = communication.GetActiveMaintenanceAnnouncement() != null;
            }
            catch
            {
                // Never take the site down because the maintenance check failed.
                underMaintenance = false;
            }

            if (!underMaintenance)
            {
                await this._next(context);
                return;
            }

            context.Response.Redirect("/Home/Maintenance");
        }

        private bool IsAlwaysAllowed(PathString path)
        {
            if (!path.HasValue)
            {
                return false;
            }

            return path.StartsWithSegments("/css")
                || path.StartsWithSegments("/js")
                || path.StartsWithSegments("/lib")
                || path.StartsWithSegments("/images")
                || path.StartsWithSegments("/uploads")
                || path.StartsWithSegments("/favicon.ico")
                || path.StartsWithSegments("/Home/Maintenance")
                || path.StartsWithSegments("/Home/Error")
                || path.StartsWithSegments("/Account/Login")
                || path.StartsWithSegments("/Account/Logout")
                || path.StartsWithSegments("/Account/AccessDenied");
        }
    }
}
