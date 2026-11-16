using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using ASI.Basecode.Resources.Constants;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Profile &amp; Settings (2.10 / 3.10 / 4.8): contact details and notification preferences.
    /// Password changes go through <see cref="IAuthService"/>.
    /// </summary>
    public class ProfileService : ServiceBase, IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly ISettingsRepository _settingsRepository;
        private readonly IBorrowingRepository _borrowings;
        private readonly IUserRepository _userRepository;
        private readonly IUserPreferenceRepository _preferences;
        private readonly IAuditLogRepository _auditLogs;

        public ProfileService(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork, IAuditLogRepository auditLogs, IUserPreferenceRepository preferences, IUserRepository userRepository, IBorrowingRepository borrowings, ISettingsRepository settingsRepository)
            : base(loggerFactory)
        {
            this._settingsRepository = settingsRepository;
            this._borrowings = borrowings;
            this._userRepository = userRepository;
            this._preferences = preferences;
            this._auditLogs = auditLogs;
            this._unitOfWork = unitOfWork;
        }

        public ProfileViewModel GetProfile(long userId)
        {
            var user = this._userRepository.GetByIdWithDetails(userId);

            if (user == null)
            {
                return null;
            }

            var preference = this.EnsurePreference(userId);

            var now = DateTime.UtcNow;
            var activeBorrowings = this._borrowings.CountForBorrowerWithoutStatuses(
                userId, BorrowingStatusCodes.Returned, BorrowingStatusCodes.Lost);

            var completed = this._borrowings.CountForBorrowerWithStatus(userId, BorrowingStatusCodes.Returned);

            var overdueCutoff = BorrowingRules.GetOverdueCutoffUtc(this._settingsRepository, now);
            var overdue = this._borrowings.CountForBorrowerInStatusesDueBefore(
                userId, BorrowingRules.OpenStatusCodes, overdueCutoff);

            return new ProfileViewModel
            {
                UserId = user.UserId,
                FirstName = user.FirstName,
                LastName = user.LastName,
                FullName = $"{user.FirstName} {user.LastName}".Trim(),
                AccountIdentifier = user.AccountIdentifier,
                SchoolEmail = user.SchoolEmail,
                RoleCode = user.Role?.RoleCode,
                RoleName = user.Role?.RoleName,
                DepartmentName = user.Department?.DepartmentName,
                AccountStatusCode = user.AccountStatus?.Code,
                AccountStatusDescription = user.AccountStatus?.Description,
                CreatedAtUtc = user.CreatedAtUtc,
                ContactNumber = user.ContactNumber,
                AlternateContact = user.AlternateContact,
                ProfileImagePath = user.ProfileImagePath,
                ActiveBorrowings = activeBorrowings,
                CompletedBorrowings = completed,
                OverdueItems = overdue,
                NotifyReservationUpdates = preference.NotifyReservationUpdates,
                NotifyDueReminders = preference.NotifyDueReminders,
                NotifyOverdueAlerts = preference.NotifyOverdueAlerts,
                NotifyReturnConfirmations = preference.NotifyReturnConfirmations,
                NotifyAnnouncements = preference.NotifyAnnouncements,
            };
        }

        public (bool Success, string Error) SaveContact(long userId, ProfileContactFormViewModel form)
        {
            try
            {
                if (form == null)
                {
                    return (false, "Please complete all required fields.");
                }

                var user = this._userRepository.GetTrackedById(userId);
                if (user == null)
                {
                    return (false, "User Account does not exist.");
                }

                var contact = form.ContactNumber?.Trim();
                if (string.IsNullOrWhiteSpace(contact))
                {
                    return (false, "Please complete all required fields.");
                }

                user.ContactNumber = contact;
                user.AlternateContact = form.AlternateContact?.Trim();

                var preference = this._preferences.GetTrackedByUserId(userId);
                if (preference == null)
                {
                    preference = new UserPreference { UserId = userId };
                    this._preferences.Add(preference);
                }

                preference.NotifyReservationUpdates = form.NotifyReservationUpdates;
                preference.NotifyDueReminders = form.NotifyDueReminders;
                preference.NotifyOverdueAlerts = form.NotifyOverdueAlerts;
                preference.NotifyReturnConfirmations = form.NotifyReturnConfirmations;
                preference.NotifyAnnouncements = form.NotifyAnnouncements;
                preference.UpdatedAtUtc = DateTime.UtcNow;

                this._auditLogs.Add(new AuditLog
                {
                    ActorUserId = userId,
                    ActionType = "UPDATE",
                    EntityType = "UserProfile",
                    EntityId = userId,
                    ReferenceNumber = user.AccountIdentifier,
                    Details = $"Updated contact details and notification preferences for '{user.FirstName} {user.LastName}'.",
                    OccurredAtUtc = DateTime.UtcNow,
                });

                this._unitOfWork.SaveChanges();
                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save profile for user {UserId}.", userId);
                return (false, "Unable to save changes. Please try again.");
            }
        }

        public string GetProfileImagePath(long userId)
        {
            return this._userRepository.GetProfileImagePath(userId);
        }

        public (bool Success, string Error, string PreviousPath) UpdateProfileImage(long userId, string imagePath)
        {
            try
            {
                var user = this._userRepository.GetTrackedById(userId);
                if (user == null)
                {
                    return (false, "User Account does not exist.", null);
                }

                var previous = user.ProfileImagePath;
                user.ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath;

                this._auditLogs.Add(new AuditLog
                {
                    ActorUserId = userId,
                    ActionType = "UPDATE",
                    EntityType = "UserProfile",
                    EntityId = userId,
                    ReferenceNumber = user.AccountIdentifier,
                    Details = user.ProfileImagePath == null
                        ? $"Removed the profile photo for '{user.FirstName} {user.LastName}'."
                        : $"Updated the profile photo for '{user.FirstName} {user.LastName}'.",
                    OccurredAtUtc = DateTime.UtcNow,
                });

                this._unitOfWork.SaveChanges();
                return (true, null, previous);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to update profile photo for user {UserId}.", userId);
                return (false, "Unable to save the photo. Please try again.", null);
            }
        }

        public bool HasCompletedTour(long userId)
        {
            return this._preferences.HasCompletedTour(userId);
        }

        public void SetTourCompleted(long userId, bool completed)
        {
            var preference = this.EnsurePreference(userId);
            preference.TourCompletedAtUtc = completed ? DateTime.UtcNow : null;
            this._unitOfWork.SaveChanges();
        }

        private UserPreference EnsurePreference(long userId)
        {
            var preference = this._preferences.GetTrackedByUserId(userId);
            if (preference != null)
            {
                return preference;
            }

            preference = new UserPreference
            {
                UserId = userId,
                NotifyReservationUpdates = true,
                NotifyDueReminders = true,
                NotifyOverdueAlerts = true,
                NotifyReturnConfirmations = true,
                NotifyAnnouncements = true,
                UpdatedAtUtc = DateTime.UtcNow,
            };

            this._preferences.Add(preference);
            this._unitOfWork.SaveChanges();
            return preference;
        }
    }
}
