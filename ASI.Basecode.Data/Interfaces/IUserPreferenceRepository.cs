using ASI.Basecode.Data.Models;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for per-user preferences (notification switches, guided tour).
    /// </summary>
    public interface IUserPreferenceRepository
    {
        /// <summary>The user's preference row (tracked), or null when none exists.</summary>
        UserPreference GetTrackedByUserId(long userId);

        /// <summary>Adds a preference row (saved by the caller's SaveChanges).</summary>
        void Add(UserPreference preference);

        /// <summary>True when the user's preference row records a completed tour.</summary>
        bool HasCompletedTour(long userId);
    }
}
