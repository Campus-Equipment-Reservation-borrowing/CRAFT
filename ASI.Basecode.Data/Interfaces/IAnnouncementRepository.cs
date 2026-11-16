using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for announcements.
    /// </summary>
    public interface IAnnouncementRepository
    {
        /// <summary>Newest announcements with their author (untracked).</summary>
        List<Announcement> GetLatestWithAuthor(int take);

        /// <summary>Unexpired announcements whose comma-separated target roles include the code (untracked), newest first.</summary>
        List<Announcement> GetActiveForRole(string roleCode, DateTime nowUtc, int take);

        Announcement GetTrackedById(long announcementId);

        /// <summary>Adds an announcement (saved by the caller).</summary>
        void Add(Announcement announcement);

        /// <summary>Removes an announcement (saved by the caller).</summary>
        void Remove(Announcement announcement);
    }
}
