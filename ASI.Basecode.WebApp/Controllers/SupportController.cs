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
    /// Support, appeal and feedback tickets: borrowers open and follow their own; custodians and
    /// admins see all, reply and resolve.
    /// </summary>
    [Authorize]
    public class SupportController : ControllerBase<SupportController>
    {
        private readonly ICommunicationService _communication;

        public SupportController(
            IHttpContextAccessor httpContextAccessor,
            ILoggerFactory loggerFactory,
            IConfiguration configuration,
            IMapper mapper,
            ICommunicationService communication)
            : base(httpContextAccessor, loggerFactory, configuration, mapper)
        {
            this._communication = communication;
        }

        public IActionResult Index(string status = null)
        {
            return this.View(this._communication.GetTickets(status));
        }

        [HttpGet]
        public IActionResult Create(string category = null)
        {
            return this.View(new SupportTicketFormViewModel { Category = category?.ToUpperInvariant() });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create(SupportTicketFormViewModel form)
        {
            if (!this.ModelState.IsValid)
            {
                return this.View(form);
            }

            var (success, error, ticketId) = this._communication.CreateTicket(form);
            if (!success)
            {
                this.ModelState.AddModelError(string.Empty, error);
                return this.View(form);
            }

            this.TempData["SuccessMessage"] = "Ticket submitted. We'll reply here and notify you.";
            return this.RedirectToAction(nameof(this.Details), new { id = ticketId });
        }

        public IActionResult Details(long id)
        {
            var model = this._communication.GetTicket(id);
            if (model == null)
            {
                this.TempData["ErrorMessage"] = ASI.Basecode.Resources.Messages.Errors.TicketNotFound;
                return this.RedirectToAction(nameof(this.Index));
            }

            return this.View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Reply(long id, string body)
        {
            var (success, error) = this._communication.Reply(id, body);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Reply sent." : error;
            return this.RedirectToAction(nameof(this.Details), new { id });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = RoleCodes.Staff)]
        public IActionResult Resolve(long id)
        {
            var (success, error) = this._communication.Resolve(id);
            this.TempData[success ? "SuccessMessage" : "ErrorMessage"] = success ? "Ticket resolved." : error;
            return this.RedirectToAction(nameof(this.Details), new { id });
        }
    }
}
