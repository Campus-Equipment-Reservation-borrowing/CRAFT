using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Services.Interfaces;
using ASI.Basecode.Services.ViewModelClasses;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;

namespace ASI.Basecode.Services.Services
{
    public class LandingService : ServiceBase, ILandingService
    {
        private readonly IUnitOfWork _unitOfWork;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public LandingService(ILoggerFactory loggerFactory, IUnitOfWork unitOfWork)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
        }

        public LandingViewModel GetLanding()
        {
            var db = this.DbContext;

            // Same available-units formula as ReservationService / EquipmentService.
            var perCategory = db.Equipment
                .Where(e => e.IsActive && e.EquipmentInventoryBalance != null)
                .GroupBy(e => e.EquipmentCategory.CategoryName)
                .Select(g => new LandingCategoryViewModel
                {
                    Name = g.Key,
                    Total = g.Sum(e => e.EquipmentInventoryBalance.TotalQuantity),
                    Available = g.Sum(e => e.EquipmentInventoryBalance.TotalQuantity
                        - e.EquipmentInventoryBalance.ReservedQuantity
                        - e.EquipmentInventoryBalance.ReleasedQuantity
                        - e.EquipmentInventoryBalance.AwaitingInspectionQuantity
                        - e.EquipmentInventoryBalance.MaintenanceQuantity
                        - e.EquipmentInventoryBalance.UnreconciledQuantity),
                })
                .ToList();

            foreach (var c in perCategory)
            {
                c.Available = Math.Max(0, c.Available);
            }

            var since = DateTime.UtcNow.AddDays(-90);
            var decisionHours = db.AuditLogs
                .Where(a => a.EntityType == "Reservation"
                    && (a.ActionType == "APPROVE" || a.ActionType == "REJECT")
                    && a.OccurredAtUtc >= since)
                .Join(db.Reservations, a => a.EntityId, r => (long?)r.ReservationId,
                    (a, r) => new { a.OccurredAtUtc, r.SubmittedAtUtc })
                .AsEnumerable()
                .Select(x => (x.OccurredAtUtc - x.SubmittedAtUtc).TotalHours)
                .Where(h => h >= 0)
                .OrderBy(h => h)
                .ToList();

            double? median = null;
            if (decisionHours.Count > 0)
            {
                var mid = decisionHours.Count / 2;
                median = decisionHours.Count % 2 == 1
                    ? decisionHours[mid]
                    : (decisionHours[mid - 1] + decisionHours[mid]) / 2;
            }

            return new LandingViewModel
            {
                ItemsInCatalog = perCategory.Sum(c => c.Total),
                CategoryCount = db.EquipmentCategories.Count(),
                MedianDecisionHours = median,
                UpdatedAtLocal = ToManila(DateTime.UtcNow),
                Categories = perCategory.OrderByDescending(c => c.Total).ToList(),
            };
        }

        private static DateTime ToManila(DateTime utc)
        {
            foreach (var id in new[] { "Asia/Manila", "Singapore Standard Time" })
            {
                try
                {
                    return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById(id));
                }
                catch (TimeZoneNotFoundException)
                {
                }
            }

            return utc.AddHours(8);
        }
    }
}
