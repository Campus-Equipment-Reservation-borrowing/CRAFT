using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    /// <summary>
    /// Builds the borrower and custodian dashboards from the operational tables.
    /// </summary>
    public class DashboardService : ServiceBase, IDashboardService
    {
        private readonly IUnitOfWork _unitOfWork;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        private readonly ICurrentUserService _currentUser;

        public DashboardService(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork, ICurrentUserService currentUser)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._currentUser = currentUser;
        }

        public BorrowerDashboardViewModel GetBorrowerDashboard(long userId)
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            var user = this.DbContext.Users
                .AsNoTracking()
                .Include(u => u.AccountStatus)
                .FirstOrDefault(u => u.UserId == userId);

            var openBorrowings = this.DbContext.Borrowings
                .AsNoTracking()
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Where(b => b.BorrowerUserId == userId
                    && b.Status.Code != "RETURNED"
                    && b.Status.Code != "LOST")
                .OrderBy(b => b.DueAtUtc)
                .ToList();

            var reservations = this.DbContext.Reservations
                .AsNoTracking()
                .Include(r => r.Equipment)
                .Include(r => r.Status)
                .Where(r => r.BorrowerUserId == userId)
                .OrderByDescending(r => r.SubmittedAtUtc)
                .ToList();

            var notifications = this.DbContext.Notifications
                .AsNoTracking()
                .Where(n => n.RecipientUserId == userId)
                .OrderByDescending(n => n.CreatedAtUtc)
                .Take(5)
                .ToList();

            var unreadCount = this.DbContext.Notifications
                .AsNoTracking()
                .Count(n => n.RecipientUserId == userId && !n.IsRead);

            var completedBorrowings = this.DbContext.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == userId && b.Status.Code == "RETURNED");

            var overdueRows = openBorrowings
                .Where(b => b.DueAtUtc < now)
                .ToList();

            var model = new BorrowerDashboardViewModel
            {
                UserId = userId,
                DisplayName = user == null ? "Borrower" : $"{user.FirstName} {user.LastName}".Trim(),
                FirstName = user?.FirstName,
                AccountStatus = user?.AccountStatus?.Code,
                VerificationNote = user?.VerificationNote,

                ActiveBorrowings = openBorrowings.Count,
                PendingReservations = reservations.Count(r => r.Status.Code == "PENDING"),
                ApprovedReady = reservations.Count(r => r.Status.Code == "APPROVED" || r.Status.Code == "READY_FOR_RELEASE"),
                OverdueItems = overdueRows.Count,
                CompletedBorrowings = completedBorrowings,
                UnreadNotifications = unreadCount,

                ActiveBorrowingRows = openBorrowings.Select(b => new BorrowerBorrowingRowViewModel
                {
                    BorrowingId = b.BorrowingId,
                    TransactionNumber = b.TransactionNumber,
                    EquipmentName = b.Equipment?.EquipmentName,
                    AssetCode = b.Equipment?.AssetCode,
                    Quantity = b.ReleasedQuantity,
                    ReleasedAtUtc = b.ReleasedAtUtc,
                    DueAtUtc = b.DueAtUtc,
                    StatusCode = b.DueAtUtc < now ? "OVERDUE" : b.Status.Code,
                    StatusDescription = b.DueAtUtc < now ? "Overdue" : b.Status.Description,
                    IsOverdue = b.DueAtUtc < now,
                    IsDueToday = b.DueAtUtc.Date == today,
                    DaysOverdue = b.DueAtUtc < now ? (int)Math.Floor((now - b.DueAtUtc).TotalDays) : 0,
                }).ToList(),

                RecentReservations = reservations.Take(5).Select(r => new BorrowerReservationRowViewModel
                {
                    ReservationId = r.ReservationId,
                    ReservationNumber = r.ReservationNumber,
                    EquipmentName = r.Equipment?.EquipmentName,
                    AssetCode = r.Equipment?.AssetCode,
                    Quantity = r.RequestedQuantity,
                    RequestedStartAtUtc = r.RequestedStartAtUtc,
                    ExpectedReturnAtUtc = r.ExpectedReturnAtUtc,
                    StatusCode = r.Status.Code,
                    StatusDescription = r.Status.Description,
                }).ToList(),

                UpcomingDue = openBorrowings.Take(4).Select(b => new BorrowerDueRowViewModel
                {
                    EquipmentName = b.Equipment?.EquipmentName,
                    AssetCode = b.Equipment?.AssetCode,
                    DueAtUtc = b.DueAtUtc,
                    StatusCode = b.DueAtUtc < now ? "OVERDUE" : b.Status.Code,
                    StatusDescription = b.DueAtUtc < now ? "Overdue" : (b.DueAtUtc.Date == today ? "Due Today" : b.Status.Description),
                    IsOverdue = b.DueAtUtc < now,
                    IsDueToday = b.DueAtUtc.Date == today,
                    DaysOverdue = b.DueAtUtc < now ? (int)Math.Floor((now - b.DueAtUtc).TotalDays) : 0,
                }).ToList(),

                Notifications = notifications.Select(n => new NotificationRowViewModel
                {
                    NotificationId = n.NotificationId,
                    NotificationType = n.NotificationType,
                    Title = n.Title,
                    Message = n.Message,
                    IsRead = n.IsRead,
                    CreatedAtUtc = n.CreatedAtUtc,
                }).ToList(),
            };

            // Recent activity = newest reservation and release events, merged.
            var activity = new List<ActivityRowViewModel>();
            activity.AddRange(reservations.Take(5).Select(r => new ActivityRowViewModel
            {
                Title = $"Reservation {r.ReservationNumber} - {r.Status.Description}",
                Detail = $"{r.Equipment?.EquipmentName} · Qty {r.RequestedQuantity}",
                OccurredAtUtc = r.CompletedAtUtc ?? r.SubmittedAtUtc,
            }));
            activity.AddRange(openBorrowings.Take(5).Select(b => new ActivityRowViewModel
            {
                Title = $"Equipment released ({b.TransactionNumber})",
                Detail = $"{b.Equipment?.EquipmentName} · Qty {b.ReleasedQuantity}",
                OccurredAtUtc = b.ReleasedAtUtc,
            }));
            model.RecentActivity = activity.OrderByDescending(a => a.OccurredAtUtc).Take(6).ToList();

            return model;
        }

        public CustodianDashboardViewModel GetCustodianDashboard()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            var policy = this.DbContext.BorrowingPolicyVersions
                .AsNoTracking()
                .Where(p => p.IsCurrent)
                .OrderByDescending(p => p.EffectiveFromUtc)
                .FirstOrDefault();
            var lowStockThreshold = policy?.LowStockThreshold ?? 2;

            // Tasks are scoped to the custodian (routed work + shared pool); inventory alerts stay global.
            var scope = this._currentUser.GetCustodianScope();

            var reservations = this.DbContext.Reservations
                .AsNoTracking()
                .Include(r => r.BorrowerUser)
                .Include(r => r.Equipment)
                .Include(r => r.Status)
                .Where(r => scope == null || r.AssignedCustodianUserId == null || r.AssignedCustodianUserId == scope)
                .ToList();

            var borrowings = this.DbContext.Borrowings
                .AsNoTracking()
                .Include(b => b.BorrowerUser)
                .Include(b => b.Equipment)
                .Include(b => b.Status)
                .Include(b => b.InspectionStatus)
                .Where(b => scope == null || b.Reservation == null
                    || b.Reservation.AssignedCustodianUserId == null
                    || b.Reservation.AssignedCustodianUserId == scope)
                .ToList();

            var equipment = this.DbContext.Equipment
                .AsNoTracking()
                .Include(e => e.EquipmentInventoryBalance)
                .Include(e => e.OperationalStatus)
                .Include(e => e.CurrentCondition)
                .Where(e => e.IsActive)
                .ToList();

            var pending = reservations.Where(r => r.Status.Code == "PENDING").ToList();

            var releasesToday = reservations
                .Where(r => (r.Status.Code == "READY_FOR_RELEASE" || r.Status.Code == "APPROVED")
                    && r.RequestedStartAtUtc.Date == today)
                .ToList();

            var openBorrowings = borrowings
                .Where(b => b.Status.Code != "RETURNED" && b.Status.Code != "LOST")
                .ToList();

            var returnsToday = openBorrowings.Where(b => b.DueAtUtc.Date == today).ToList();
            var overdue = openBorrowings.Where(b => b.DueAtUtc < now).ToList();
            var awaitingInspection = borrowings
                .Where(b => b.Status.Code == "RETURNED"
                    && string.Equals(b.InspectionStatus?.Code, "PENDING", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var alerts = new List<InventoryAlertRowViewModel>();
            foreach (var e in equipment)
            {
                var balance = e.EquipmentInventoryBalance;
                var available = balance == null
                    ? 0
                    : balance.TotalQuantity - balance.ReservedQuantity - balance.ReleasedQuantity
                      - balance.AwaitingInspectionQuantity - balance.MaintenanceQuantity - balance.UnreconciledQuantity;

                var underMaintenance = string.Equals(e.OperationalStatus?.Code, "UNDER_MAINTENANCE", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(e.CurrentCondition?.Code, "UNDER_MAINTENANCE", StringComparison.OrdinalIgnoreCase);
                var lowStock = !underMaintenance && available <= lowStockThreshold;

                if (underMaintenance || lowStock)
                {
                    alerts.Add(new InventoryAlertRowViewModel
                    {
                        EquipmentId = e.EquipmentId,
                        EquipmentName = e.EquipmentName,
                        AssetCode = e.AssetCode,
                        AvailableUnits = available,
                        TotalQuantity = balance?.TotalQuantity ?? 0,
                        OperationalStatus = e.OperationalStatus?.Description,
                        IsLowStock = lowStock,
                        IsUnderMaintenance = underMaintenance,
                    });
                }
            }

            var tasks = new List<CustodianTaskRowViewModel>();
            tasks.AddRange(pending.OrderBy(r => r.SubmittedAtUtc).Take(5).Select(r => new CustodianTaskRowViewModel
            {
                Type = "REVIEW",
                Title = $"Review reservation {r.ReservationNumber}",
                Detail = $"{r.BorrowerUser?.FirstName} {r.BorrowerUser?.LastName} · {r.Equipment?.EquipmentName}",
                WhenUtc = r.SubmittedAtUtc,
                LinkController = "Custodian",
                LinkAction = "Review",
                LinkId = r.ReservationId,
            }));
            tasks.AddRange(releasesToday.Take(4).Select(r => new CustodianTaskRowViewModel
            {
                Type = "RELEASE",
                Title = $"Release {r.ReservationNumber}",
                Detail = $"{r.BorrowerUser?.FirstName} {r.BorrowerUser?.LastName} · {r.Equipment?.EquipmentName}",
                WhenUtc = r.RequestedStartAtUtc,
                LinkController = "Custodian",
                LinkAction = "Release",
                LinkId = r.ReservationId,
            }));
            tasks.AddRange(returnsToday.Take(4).Select(b => new CustodianTaskRowViewModel
            {
                Type = "RETURN",
                Title = $"Return due - {b.TransactionNumber}",
                Detail = $"{b.BorrowerUser?.FirstName} {b.BorrowerUser?.LastName} · {b.Equipment?.EquipmentName}",
                WhenUtc = b.DueAtUtc,
                LinkController = "Returns",
                LinkAction = "Create",
                LinkId = b.BorrowingId,
            }));
            tasks.AddRange(overdue.OrderBy(b => b.DueAtUtc).Take(4).Select(b => new CustodianTaskRowViewModel
            {
                Type = "OVERDUE",
                Title = $"Overdue - {b.TransactionNumber}",
                Detail = $"{b.BorrowerUser?.FirstName} {b.BorrowerUser?.LastName} · {b.Equipment?.EquipmentName}",
                WhenUtc = b.DueAtUtc,
                LinkController = "Custodian",
                LinkAction = "Borrowings",
                LinkId = null,
            }));

            return new CustodianDashboardViewModel
            {
                PendingReservations = pending.Count,
                ScheduledReleasesToday = releasesToday.Count,
                ActiveBorrowings = openBorrowings.Count,
                ExpectedReturnsToday = returnsToday.Count,
                OverdueEquipment = overdue.Count,
                AwaitingInspection = awaitingInspection.Count,
                LowStockCount = alerts.Count(a => a.IsLowStock),
                UnderMaintenanceCount = alerts.Count(a => a.IsUnderMaintenance),
                LowStockThreshold = lowStockThreshold,
                InventoryAlerts = alerts.OrderByDescending(a => a.IsUnderMaintenance).ThenBy(a => a.AvailableUnits).Take(8).ToList(),
                Tasks = tasks.OrderBy(t => t.WhenUtc).Take(10).ToList(),
            };
        }
    }
}
