using ASI.Basecode.Services.ViewModelClasses;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Profile &amp; Settings read/write model shared by 2.10, 3.10 and 4.8.
    /// </summary>
    public interface IProfileService
    {
        ProfileViewModel GetProfile(long userId);

        string GetProfileImagePath(long userId);

        (bool Success, string Error) SaveContact(long userId, ProfileContactFormViewModel form);

        (bool Success, string Error, string PreviousImagePath) SaveProfileImage(long userId, string imagePath);
    }
}
