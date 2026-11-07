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
using System.Linq;
using ErrorMessages = ASI.Basecode.Resources.Messages.Errors;

namespace ASI.Basecode.Services.Services
{
    public class SettingsService : ServiceBase, ISettingsService
    {
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
                    OverdueGracePeriodMinutes = policy.OverdueGracePeriodMinutes,
                    LowStockThreshold = policy.LowStockThreshold,
                    BlockReservationWhenOverdue = policy.BlockReservationWhenOverdue,
                },
                PolicyHistory = this._repository.GetPolicyHistory(),
                Categories = this._repository.GetCategories(),
                Locations = this._repository.GetLocations(),
            };
        }

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
                if (previous != null)
                {
                    previous.IsCurrent = false;
                    // Persist the clear before inserting the new current row so the filtered unique
                    // index on is_current never sees two current rows in one batch.
                    this._unitOfWork.SaveChanges();
                }

                var nextVersion = "v1.0";
                if (previous != null && !string.IsNullOrWhiteSpace(previous.VersionNumber))
                {
                    var v = previous.VersionNumber.TrimStart('v', 'V');
                    var parts = v.Split('.');
                    if (parts.Length == 2 && int.TryParse(parts[0], out var major) && int.TryParse(parts[1], out var minor))
                    {
                        nextVersion = $"v{major}.{minor + 1}";
                    }
                }

                var newPolicy = new BorrowingPolicyVersion
                {
                    VersionNumber = nextVersion,
                    EffectiveFromUtc = form.EffectiveFromUtc == default ? DateTime.UtcNow : DateTime.SpecifyKind(form.EffectiveFromUtc, DateTimeKind.Utc),
                    IsCurrent = true,
                    MaxBorrowDurationDays = form.MaxBorrowDurationDays,
                    MaxActiveBorrowingUnits = form.MaxActiveBorrowingUnits,
                    OverdueGracePeriodMinutes = form.OverdueGracePeriodMinutes,
                    LowStockThreshold = form.LowStockThreshold,
                    BlockReservationWhenOverdue = form.BlockReservationWhenOverdue,
                };

                this.DbContext.BorrowingPolicyVersions.Add(newPolicy);
                this._unitOfWork.SaveChanges();

                this.DbContext.AuditLogs.Add(new AuditLog
                {
                    ActorUserId = this._currentUser.GetActorUserId(),
                    ActionType = "UPDATE",
                    EntityType = "BorrowingPolicyVersion",
                    EntityId = newPolicy.PolicyVersionId,
                    ReferenceNumber = newPolicy.VersionNumber,
                    Details = $"Published policy {newPolicy.VersionNumber}: max {newPolicy.MaxBorrowDurationDays} days, {newPolicy.MaxActiveBorrowingUnits} active units.",
                    OccurredAtUtc = DateTime.UtcNow,
                });
                this._unitOfWork.SaveChanges();

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to save policy.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) AddCategory(string code, string name)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                return (false, ErrorMessages.ServerError);
            }
            try
            {
                this.DbContext.EquipmentCategories.Add(new EquipmentCategory { CategoryCode = code.Trim(), CategoryName = name.Trim() });
                this._unitOfWork.SaveChanges();
                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to add category.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) DeleteCategory(long categoryId)
        {
            if (this._repository.CategoryHasEquipment(categoryId))
            {
                return (false, ErrorMessages.ServerError);
            }
            try
            {
                var entity = this.DbContext.EquipmentCategories.FirstOrDefault(c => c.EquipmentCategoryId == categoryId);
                if (entity == null)
                {
                    return (false, ErrorMessages.ServerError);
                }
                this.DbContext.EquipmentCategories.Remove(entity);
                this._unitOfWork.SaveChanges();
                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete category.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) AddLocation(string code, string name, bool isPickup)
        {
            if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(name))
            {
                return (false, ErrorMessages.ServerError);
            }
            try
            {
                this.DbContext.Locations.Add(new Location
                {
                    LocationCode = code.Trim(),
                    LocationName = name.Trim(),
                    IsPickupLocation = isPickup,
                    IsActive = true,
                });
                this._unitOfWork.SaveChanges();
                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to add location.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) DeleteLocation(long locationId)
        {
            if (this._repository.LocationHasEquipment(locationId))
            {
                return (false, ErrorMessages.ServerError);
            }
            try
            {
                var entity = this.DbContext.Locations.FirstOrDefault(l => l.LocationId == locationId);
                if (entity == null)
                {
                    return (false, ErrorMessages.ServerError);
                }
                this.DbContext.Locations.Remove(entity);
                this._unitOfWork.SaveChanges();
                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete location.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public NotificationsIndexViewModel GetNotifications(
            long userId,
            string search = null,
            string status = null,
            string type = null,
            int page = 1,
            int pageSize = 10)
        {
            const int DefaultPageSize = 10;
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var query = this.DbContext.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId);

            var unreadCount = query.Count(n => !n.IsRead);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim();
                query = query.Where(n => n.Title.Contains(term) || n.Message.Contains(term));
            }

            if (string.Equals(status, "unread", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => !n.IsRead);
            }
            else if (string.Equals(status, "read", StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(n => n.IsRead);
            }

            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "all", StringComparison.OrdinalIgnoreCase))
            {
                var typeCode = type.Trim().ToUpperInvariant();
                query = query.Where(n => n.NotificationType == typeCode);
            }

            var totalCount = query.Count();

            var items = query
                .OrderByDescending(n => n.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(n => new NotificationRowViewModel
                {
                    NotificationId = n.NotificationId,
                    NotificationType = n.NotificationType,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc,
                })
                .ToList();

            return new NotificationsIndexViewModel
            {
                Notifications = items,
                Page = new PagedResult<NotificationRowViewModel>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                },
                UnreadCount = unreadCount,
                Search = search,
                Status = string.IsNullOrWhiteSpace(status) ? "all" : status,
                Type = string.IsNullOrWhiteSpace(type) ? "all" : type,
                TypeOptions = ASI.Basecode.Resources.Constants.NotificationTypes.All.ToList(),
            };
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
    }
}
