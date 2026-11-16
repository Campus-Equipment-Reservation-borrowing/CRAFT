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
        private readonly IEquipmentRepository _equipment;
        private readonly IAuditLogRepository _auditLogs;

        public LandingService(ILoggerFactory loggerFactory, IEquipmentRepository equipment, IAuditLogRepository auditLogs)
            : base(loggerFactory)
        {
            this._auditLogs = auditLogs;
            this._equipment = equipment;
        }

        public LandingViewModel GetLanding()
        {
            // Same available-units formula as ReservationService / EquipmentService.
            var perCategory = this._equipment.GetActiveCategoryUnitTotals()
                .Select(c => new LandingCategoryViewModel { Name = c.Name, Total = c.Total, Available = c.Available })
                .ToList();

            foreach (var c in perCategory)
            {
                c.Available = Math.Max(0, c.Available);
            }

            var since = DateTime.UtcNow.AddDays(-90);
            var decisionHours = this._auditLogs
                .GetReservationDecisionTimes("Reservation", new[] { "APPROVE", "REJECT" }, since)
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
                CategoryCount = this._equipment.CountCategories(),
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
