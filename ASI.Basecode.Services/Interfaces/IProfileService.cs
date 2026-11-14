using ASI.Basecode.Services.ViewModelClasses;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Profile &amp; Settings read/write model shared by 2.10, 3.10 and 4.8.
    /// </summary>
    public interface IProfileService
    {
        ProfileViewModel GetProfile(long userId);

        (bool Success, string Error) SaveContact(long userId, ProfileContactFormViewModel form);

        /// <summary>Whether the user has finished or skipped the first-sign-in walkthrough.</summary>
        bool HasCompletedTour(long userId);

        /// <summary>Records the walkthrough as done (completed = true) or resets it so it plays again.</summary>
        void SetTourCompleted(long userId, bool completed);
    }
}
