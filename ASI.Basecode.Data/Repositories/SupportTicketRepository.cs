using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class SupportTicketRepository : BaseRepository, ISupportTicketRepository
    {
        public SupportTicketRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        private IQueryable<SupportTicket> Visible(long? requesterUserId)
        {
            var query = this.Context.SupportTickets.AsNoTracking().AsQueryable();
            if (requesterUserId.HasValue)
            {
                var id = requesterUserId.Value;
                query = query.Where(t => t.RequesterUserId == id);
            }

            return query;
        }

        public int CountWithStatus(long? requesterUserId, string status)
        {
            return this.Visible(requesterUserId).Count(t => t.Status == status);
        }

        public List<SupportTicket> GetList(long? requesterUserId, string statusFilter, string firstStatus, int take)
        {
            var query = this.Visible(requesterUserId);
            if (statusFilter != null)
            {
                query = query.Where(t => t.Status == statusFilter);
            }

            return query
                .Include(t => t.RequesterUser)
                .Include(t => t.Replies)
                .OrderBy(t => t.Status == firstStatus ? 0 : 1)
                .ThenByDescending(t => t.Replies.Max(r => (DateTime?)r.CreatedAtUtc) ?? t.CreatedAtUtc)
                .Take(take)
                .ToList();
        }

        public SupportTicket GetWithDetails(long ticketId, long? requesterUserId)
        {
            return this.Visible(requesterUserId)
                .Include(t => t.RequesterUser).ThenInclude(u => u.AccountStatus)
                .Include(t => t.Replies).ThenInclude(r => r.AuthorUser).ThenInclude(u => u.Role)
                .FirstOrDefault(t => t.TicketId == ticketId);
        }

        public SupportTicket GetTracked(long ticketId, long? requesterUserId)
        {
            return this.Context.SupportTickets
                .FirstOrDefault(t => t.TicketId == ticketId && (requesterUserId == null || t.RequesterUserId == requesterUserId));
        }

        public void Add(SupportTicket ticket)
        {
            this.Context.SupportTickets.Add(ticket);
        }

        public void AddReply(SupportTicketReply reply)
        {
            this.Context.SupportTicketReplies.Add(reply);
        }
    }
}
