using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    public class AnnouncementRepository : BaseRepository, IAnnouncementRepository
    {
        public AnnouncementRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public List<Announcement> GetLatestWithAuthor(int take)
        {
            return this.Context.Announcements.AsNoTracking()
                .Include(a => a.CreatedByUser)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(take)
                .ToList();
        }

        public List<Announcement> GetActiveForRole(string roleCode, DateTime nowUtc, int take)
        {
            // target_roles is a short comma list; match whole codes only.
            return this.Context.Announcements.AsNoTracking()
                .Where(a => a.ExpiresAtUtc == null || a.ExpiresAtUtc > nowUtc)
                .Where(a => ("," + a.TargetRoles + ",").Contains("," + roleCode + ","))
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(take)
                .ToList();
        }

        public Announcement GetTrackedById(long announcementId)
        {
            return this.Context.Announcements.FirstOrDefault(a => a.AnnouncementId == announcementId);
        }

        public void Add(Announcement announcement)
        {
            this.Context.Announcements.Add(announcement);
        }

        public void Remove(Announcement announcement)
        {
            this.Context.Announcements.Remove(announcement);
        }
    }
}
