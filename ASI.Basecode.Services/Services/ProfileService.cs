using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.Manager;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Profile &amp; Settings (2.10 / 3.10 / 4.8): contact details and notification preferences.
    /// Password changes go through <see cref="IAuthService"/>.
    /// </summary>
    public class ProfileService : ServiceBase, IProfileService
    {
        private readonly IUnitOfWork _unitOfWork;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public ProfileService(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
        }

        public ProfileViewModel GetProfile(long userId)
        {
            var user = this.DbContext.Users
                .AsNoTracking()
                .Include(u => u.Role)
                .Include(u => u.Department)
                .Include(u => u.AccountStatus)
                .FirstOrDefault(u => u.UserId == userId);

            if (user == null)
            {
                return null;
            }

            var preference = this.EnsurePreference(userId);

            var now = DateTime.UtcNow;
            var activeBorrowings = this.DbContext.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == userId
                    && b.Status.Code != "RETURNED"
                    && b.Status.Code != "LOST");

            var completed = this.DbContext.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == userId && b.Status.Code == "RETURNED");

            var overdueCutoff = BorrowingRules.GetOverdueCutoffUtc(this.DbContext, now);
            var overdue = this.DbContext.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == userId
                    && BorrowingRules.OpenStatusCodes.Contains(b.Status.Code)
                    && b.DueAtUtc < overdueCutoff);

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

                var user = this.DbContext.Users.FirstOrDefault(u => u.UserId == userId);
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

                var preference = this.DbContext.UserPreferences.FirstOrDefault(p => p.UserId == userId);
                if (preference == null)
                {
                    preference = new UserPreference { UserId = userId };
                    this.DbContext.UserPreferences.Add(preference);
                }

                preference.NotifyReservationUpdates = form.NotifyReservationUpdates;
                preference.NotifyDueReminders = form.NotifyDueReminders;
                preference.NotifyOverdueAlerts = form.NotifyOverdueAlerts;
                preference.NotifyReturnConfirmations = form.NotifyReturnConfirmations;
                preference.NotifyAnnouncements = form.NotifyAnnouncements;
                preference.UpdatedAtUtc = DateTime.UtcNow;

                this.DbContext.AuditLogs.Add(new AuditLog
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
            return this.DbContext.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => u.ProfileImagePath)
                .FirstOrDefault();
        }

        public (bool Success, string Error, string PreviousPath) UpdateProfileImage(long userId, string imagePath)
        {
            try
            {
                var user = this.DbContext.Users.FirstOrDefault(u => u.UserId == userId);
                if (user == null)
                {
                    return (false, "User Account does not exist.", null);
                }

                var previous = user.ProfileImagePath;
                user.ProfileImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath;

                this.DbContext.AuditLogs.Add(new AuditLog
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
            return this.DbContext.UserPreferences.AsNoTracking()
                .Any(p => p.UserId == userId && p.TourCompletedAtUtc != null);
        }

        public void SetTourCompleted(long userId, bool completed)
        {
            var preference = this.EnsurePreference(userId);
            preference.TourCompletedAtUtc = completed ? DateTime.UtcNow : null;
            this._unitOfWork.SaveChanges();
        }

        private UserPreference EnsurePreference(long userId)
        {
            var preference = this.DbContext.UserPreferences.FirstOrDefault(p => p.UserId == userId);
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

            this.DbContext.UserPreferences.Add(preference);
            this._unitOfWork.SaveChanges();
            return preference;
        }
    }
}
