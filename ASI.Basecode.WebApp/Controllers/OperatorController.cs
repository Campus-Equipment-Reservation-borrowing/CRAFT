using ASI.Basecode.WebApp.Mvc;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using ASI.Basecode.Resources.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace ASI.Basecode.WebApp.Controllers
{
    /// <summary>
    /// Retired operator switcher. It predates cookie authentication: it only wrote session keys,
    /// which <see cref="CurrentUserService"/> ignores for signed-in users, so "acting as" someone
    /// never took effect. Making it real would mean admin impersonation, which breaks the audit
    /// trail, so the old URLs now explain how to act as another account instead.
    /// </summary>
    [Authorize(Roles = RoleCodes.Admin)]
    public class OperatorController : ControllerBase<OperatorController>
    {
        private const string RetiredMessage =
            "Operator switching is no longer available. Actions are always recorded against the signed-in account; " +
            "to act as another user, sign out and sign in with that account.";

        public OperatorController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
        }

        public IActionResult Index(string returnUrl)
        {
            this.TempData["ErrorMessage"] = RetiredMessage;
            return this.RedirectToAction("Index", "Users");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Select(long userId, string returnUrl)
        {
            this.TempData["ErrorMessage"] = RetiredMessage;
            return this.RedirectToAction("Index", "Users");
        }
    }
}
