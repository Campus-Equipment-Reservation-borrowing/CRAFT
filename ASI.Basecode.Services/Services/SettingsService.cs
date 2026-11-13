using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.Repositories;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class SettingsService : ServiceBase, ISettingsService
    {
        private static readonly Regex CodePattern = new Regex(@"^[A-Z0-9_-]{2,50}$");

        private readonly IUnitOfWork _unitOfWork;
        private readonly ISettingsRepository _repository;
        private readonly ICurrentUserService _currentUser;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public SettingsService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            ISettingsRepository repository,
            ICurrentUserService currentUser)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._repository = repository;
            this._currentUser = currentUser;
        }

        public SettingsIndexViewModel GetSettings()
        {
            var policy = this._repository.GetCurrentPolicy();
            return new SettingsIndexViewModel
            {
                Policy = policy == null ? new PolicyFormViewModel() : new PolicyFormViewModel
                {
                    PolicyVersionId = policy.PolicyVersionId,
                    VersionNumber = policy.VersionNumber,
                    EffectiveFromUtc = policy.EffectiveFromUtc,
                    MaxBorrowDurationDays = policy.MaxBorrowDurationDays,
                    MaxActiveBorrowingUnits = policy.MaxActiveBorrowingUnits,
                    MaxUnitsPerBorrower = policy.MaxUnitsPerBorrower,
                    OverdueGracePeriodMinutes = policy.OverdueGracePeriodMinutes,
                    LowStockThreshold = policy.LowStockThreshold,
                    BlockReservationWhenOverdue = policy.BlockReservationWhenOverdue,
                },
                PolicyHistory = this._repository.GetPolicyHistory(),
                Categories = this._repository.GetCategories(),
                Locations = this._repository.GetLocations(),
            };
        }

        /// <summary>Publishes a new policy version. On success, Error carries the confirmation text.</summary>
        public (bool Success, string Error) SavePolicy(PolicyFormViewModel form)
        {
            if (form == null)
            {
                return (false, ErrorMessages.ServerError);
            }

            try
            {
                // versioning: never update — create a new row, mark previous current=false
                var previous = this._repository.GetCurrentPolicy();
                if (previous != null
                    && previous.MaxBorrowDurationDays == form.MaxBorrowDurationDays
                    && previous.MaxActiveBorrowingUnits == form.MaxActiveBorrowingUnits
                    && previous.MaxUnitsPerBorrower == form.MaxUnitsPerBorrower
                    && previous.OverdueGracePeriodMinutes == form.OverdueGracePeriodMinutes
                    && previous.LowStockThreshold == form.LowStockThreshold
                    && previous.BlockReservationWhenOverdue == form.BlockReservationWhenOverdue)
                {
                    return (false, "No changes to publish. The values match the current policy.");
                }

                if (previous != null)
                {
                    previous.IsCurrent = false;
                }

                var nextVersion = "v1.0";
                if (previous != null && !string.IsNullOrWhiteSpace(previous.VersionNumber))
                {
                    var v = previous.VersionNumber.TrimStart('v', 'V');
                    if (decimal.TryParse(v, NumberStyles.Number, CultureInfo.InvariantCulture, out var n))
                    {
                        nextVersion = "v" + (n + 0.1m).ToString("0.0", CultureInfo.InvariantCulture);
                    }
                }

                var newPolicy = new BorrowingPolicyVersion
                {
                    VersionNumber = nextVersion,
                    EffectiveFromUtc = DateTime.UtcNow,
                    IsCurrent = true,
                    MaxBorrowDurationDays = form.MaxBorrowDurationDays,
                    MaxActiveBorrowingUnits = form.MaxActiveBorrowingUnits,
                    MaxUnitsPerBorrower = form.MaxUnitsPerBorrower,
                    OverdueGracePeriodMinutes = form.OverdueGracePeriodMinutes,
                    LowStockThreshold = form.LowStockThreshold,
                    BlockReservationWhenOverdue = form.BlockReservationWhenOverdue,
                };

                this.DbContext.BorrowingPolicyVersions.Add(newPolicy);
                this._unitOfWork.SaveChanges();

                var summary = $"{newPolicy.MaxBorrowDurationDays} days max, {newPolicy.MaxUnitsPerBorrower} units per reservation, {newPolicy.MaxActiveBorrowingUnits} open requests";
                this.Audit("UPDATE", "BorrowingPolicyVersion", newPolicy.PolicyVersionId, newPolicy.VersionNumber,
                    $"Published policy {newPolicy.VersionNumber}: {summary}.");

                return (true, $"Policy {newPolicy.VersionNumber} published: {summary}.");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save policy.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) AddCategory(string code, string name)
        {
            code = code?.Trim().ToUpperInvariant();
            name = name?.Trim();
            var invalid = ValidateCodeAndName(code, name, "category");
            if (invalid != null)
            {
                return (false, invalid);
            }

            if (this.DbContext.EquipmentCategories.Any(c => c.CategoryCode.ToUpper() == code))
            {
                return (false, $"Category code {code} already exists.");
            }

            var upperName = name.ToUpper();
            if (this.DbContext.EquipmentCategories.Any(c => c.CategoryName.ToUpper() == upperName))
            {
                return (false, $"A category named \"{name}\" already exists.");
            }

            try
            {
                var entity = new EquipmentCategory { CategoryCode = code, CategoryName = name };
                this.DbContext.EquipmentCategories.Add(entity);
                this._unitOfWork.SaveChanges();
                this.Audit("CREATE", "EquipmentCategory", entity.EquipmentCategoryId, code, $"Added equipment category {code} ({name}).");
                return (true, $"Category {code} added.");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to add category.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) DeleteCategory(long categoryId)
        {
            var entity = this.DbContext.EquipmentCategories.FirstOrDefault(c => c.EquipmentCategoryId == categoryId);
            if (entity == null)
            {
                return (false, "That category no longer exists.");
            }

            var inUse = this.DbContext.Equipment.Count(e => e.EquipmentCategoryId == categoryId);
            if (inUse > 0)
            {
                return (false, $"Category {entity.CategoryCode} is used by {inUse} equipment record(s). Reassign them first.");
            }

            try
            {
                this.DbContext.EquipmentCategories.Remove(entity);
                this._unitOfWork.SaveChanges();
                this.Audit("DELETE", "EquipmentCategory", categoryId, entity.CategoryCode, $"Deleted equipment category {entity.CategoryCode} ({entity.CategoryName}).");
                return (true, $"Category {entity.CategoryCode} deleted.");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete category.");
                this.DbContext.Entry(entity).State = EntityState.Unchanged;
                return (false, $"Category {entity.CategoryCode} is still referenced elsewhere (e.g. custodian assignments) and can't be deleted.");
            }
        }

        public (bool Success, string Error) AddLocation(string code, string name, bool isPickup)
        {
            code = code?.Trim().ToUpperInvariant();
            name = name?.Trim();
            var invalid = ValidateCodeAndName(code, name, "location");
            if (invalid != null)
            {
                return (false, invalid);
            }

            if (this.DbContext.Locations.Any(l => l.LocationCode.ToUpper() == code))
            {
                return (false, $"Location code {code} already exists.");
            }

            try
            {
                var entity = new Location
                {
                    LocationCode = code,
                    LocationName = name,
                    IsPickupLocation = isPickup,
                    IsActive = true,
                };
                this.DbContext.Locations.Add(entity);
                this._unitOfWork.SaveChanges();
                this.Audit("CREATE", "Location", entity.LocationId, code, $"Added location {code} ({name}).");
                return (true, $"Location {code} added.");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to add location.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) DeleteLocation(long locationId)
        {
            var entity = this.DbContext.Locations.FirstOrDefault(l => l.LocationId == locationId);
            if (entity == null)
            {
                return (false, "That location no longer exists.");
            }

            if (this._repository.LocationHasEquipment(locationId))
            {
                return (false, $"Location {entity.LocationCode} is used by equipment. Move that equipment first.");
            }

            try
            {
                this.DbContext.Locations.Remove(entity);
                this._unitOfWork.SaveChanges();
                this.Audit("DELETE", "Location", locationId, entity.LocationCode, $"Deleted location {entity.LocationCode} ({entity.LocationName}).");
                return (true, $"Location {entity.LocationCode} deleted.");
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete location.");
                this.DbContext.Entry(entity).State = EntityState.Unchanged;
                return (false, $"Location {entity.LocationCode} is still referenced (e.g. by reservations) and can't be deleted.");
            }
        }

        public NotificationsIndexViewModel GetNotifications(long userId)
        {
            var items = this.DbContext.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(100)
                .Select(n => new NotificationRowViewModel
                {
                    NotificationId = n.NotificationId,
                    NotificationType = n.NotificationType,
                    Title = n.Title,
                    Message = n.Message,
                    LinkUrl = n.LinkUrl,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc,
                })
                .ToList();

            return new NotificationsIndexViewModel
            {
                Notifications = items,
                UnreadCount = items.Count(n => !n.IsRead),
            };
        }

        /// <summary>Marks the user's notification read and returns where it should open, or null if it isn't theirs.</summary>
        public string OpenNotification(long notificationId, long userId)
        {
            var entity = this.DbContext.Notifications.FirstOrDefault(n => n.NotificationId == notificationId && n.RecipientUserId == userId);
            if (entity == null)
            {
                return null;
            }

            if (!entity.IsRead)
            {
                entity.IsRead = true;
                this._unitOfWork.SaveChanges();
            }

            return entity.LinkUrl ?? FallbackLink(entity.NotificationType);
        }

        public void MarkNotificationRead(long notificationId, long userId)
        {
            var entity = this.DbContext.Notifications.FirstOrDefault(n => n.NotificationId == notificationId && n.RecipientUserId == userId);
            if (entity != null)
            {
                entity.IsRead = true;
                this._unitOfWork.SaveChanges();
            }
        }

        public void MarkAllNotificationsRead(long userId)
        {
            var unread = this.DbContext.Notifications.Where(n => n.RecipientUserId == userId && !n.IsRead).ToList();
            foreach (var n in unread)
            {
                n.IsRead = true;
            }
            this._unitOfWork.SaveChanges();
        }

        /// <summary>Destination for legacy notifications saved before links existed.</summary>
        public static string FallbackLink(string notificationType)
        {
            switch ((notificationType ?? string.Empty).ToUpperInvariant())
            {
                case "SUPPORT": return "/Support";
                case "RESERVATION": return "/Reservations/Mine";
                case "ANNOUNCEMENT": return "/";
                case "ACCOUNT": return "/Profile";
                default: return null;
            }
        }

        private static string ValidateCodeAndName(string code, string name, string kind)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                return $"Enter both a {kind} code and a {kind} name.";
            }

            if (!CodePattern.IsMatch(code))
            {
                return "Code must be 2–50 characters: letters, digits, dashes or underscores (e.g. LAPTOP).";
            }

            if (name.Length > 200)
            {
                return "Name must be at most 200 characters.";
            }

            return null;
        }

        private void Audit(string action, string entityType, long entityId, string reference, string details)
        {
            this.DbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = this._currentUser.GetActorUserId(),
                ActionType = action,
                EntityType = entityType,
                EntityId = entityId,
                ReferenceNumber = reference,
                Details = details,
                OccurredAtUtc = DateTime.UtcNow,
            });
            this._unitOfWork.SaveChanges();
        }
    }
}
