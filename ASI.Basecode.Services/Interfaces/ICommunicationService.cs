using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Admin announcements and the borrower → staff support / appeal channel.
    /// </summary>
    public interface ICommunicationService
    {
        AnnouncementIndexViewModel GetAnnouncements();

        /// <summary>Publishes an announcement and notifies every active user in the target roles (respecting preferences).</summary>
        (bool Success, string Error) PublishAnnouncement(AnnouncementFormViewModel form);

        (bool Success, string Error) DeleteAnnouncement(long announcementId);

        /// <summary>Unexpired announcements addressed to <paramref name="roleCode"/> (newest first).</summary>
        List<AnnouncementRowViewModel> GetActiveAnnouncementsFor(string roleCode, int take = 3);

        SupportTicketIndexViewModel GetTickets(string status);

        /// <summary>Opens a ticket for the signed-in user and notifies administrators.</summary>
        (bool Success, string Error, long TicketId) CreateTicket(SupportTicketFormViewModel form);

        /// <summary>Null when the ticket doesn't exist or the actor may not see it.</summary>
        SupportTicketDetailsViewModel GetTicket(long ticketId);

        (bool Success, string Error) Reply(long ticketId, string body);

        (bool Success, string Error) Resolve(long ticketId);

        VerificationStatsViewModel GetVerificationStats();

        BorrowerHistoryViewModel GetBorrowerHistory(long userId);
    }
}
