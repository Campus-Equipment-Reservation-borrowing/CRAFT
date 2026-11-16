using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// User preference repository implementation. Data access only.
    /// </summary>
    public class UserPreferenceRepository : BaseRepository, IUserPreferenceRepository
    {
        public UserPreferenceRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public UserPreference GetTrackedByUserId(long userId)
        {
            return this.Context.UserPreferences.FirstOrDefault(p => p.UserId == userId);
        }

        public void Add(UserPreference preference)
        {
            this.Context.UserPreferences.Add(preference);
        }

        public bool HasCompletedTour(long userId)
        {
            return this.Context.UserPreferences.AsNoTracking()
                .Any(p => p.UserId == userId && p.TourCompletedAtUtc != null);
        }
    }
}
