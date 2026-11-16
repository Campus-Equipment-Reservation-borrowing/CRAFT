using ASI.Basecode.Data.Models;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for support tickets and their replies. A null requester id means "any requester";
    /// the caller decides who may see everyone's tickets.
    /// </summary>
    public interface ISupportTicketRepository
    {
        int CountWithStatus(long? requesterUserId, string status);

        /// <summary>Tickets (untracked, with requester and replies), optionally of one status; tickets in
        /// <paramref name="firstStatus"/> first, then by latest activity.</summary>
        List<SupportTicket> GetList(long? requesterUserId, string statusFilter, string firstStatus, int take);

        /// <summary>One ticket (untracked) with requester (+status) and replies (+author role), or null.</summary>
        SupportTicket GetWithDetails(long ticketId, long? requesterUserId);

        SupportTicket GetTracked(long ticketId, long? requesterUserId);

        /// <summary>Adds a ticket (saved by the caller).</summary>
        void Add(SupportTicket ticket);

        /// <summary>Adds a reply (saved by the caller).</summary>
        void AddReply(SupportTicketReply reply);
    }
}
