using ASI.Basecode.Data;
using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using ASI.Basecode.Resources.Constants;
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
    /// <inheritdoc cref="ICommunicationService" />
    public class CommunicationService : ServiceBase, ICommunicationService
    {
        private static readonly string[] AllRoles = { RoleCodes.Student, RoleCodes.Faculty, RoleCodes.Technician, RoleCodes.Admin };

        private readonly IUnitOfWork _unitOfWork;
        private readonly ICurrentUserService _currentUser;
        private readonly INotificationDispatcher _notifications;

        private AllianceDbContext DbContext => (AllianceDbContext)_unitOfWork.Database;

        public CommunicationService(
            ILoggerFactory loggerFactory,
            IUnitOfWork unitOfWork,
            ICurrentUserService currentUser,
            INotificationDispatcher notifications)
            : base(loggerFactory)
        {
            this._unitOfWork = unitOfWork;
            this._currentUser = currentUser;
            this._notifications = notifications;
        }

        // ---------------------------------------------------------------- announcements

        public AnnouncementIndexViewModel GetAnnouncements()
        {
            var now = DateTime.UtcNow;
            return new AnnouncementIndexViewModel
            {
                Items = this.DbContext.Announcements.AsNoTracking()
                    .OrderByDescending(a => a.CreatedAtUtc)
                    .Take(50)
                    .Select(a => new AnnouncementRowViewModel
                    {
                        AnnouncementId = a.AnnouncementId,
                        Title = a.Title,
                        Body = a.Body,
                        TargetRoles = a.TargetRoles,
                        CreatedByName = a.CreatedByUser == null ? null : a.CreatedByUser.FirstName + " " + a.CreatedByUser.LastName,
                        CreatedAtUtc = a.CreatedAtUtc,
                        ExpiresAtUtc = a.ExpiresAtUtc,
                        IsActive = a.ExpiresAtUtc == null || a.ExpiresAtUtc > now,
                    })
                    .ToList()
                    .Select(WithAudienceLabel)
                    .ToList(),
            };
        }

        public (bool Success, string Error) PublishAnnouncement(AnnouncementFormViewModel form)
        {
            try
            {
                var roles = (form?.TargetRoles ?? new List<string>())
                    .Select(r => r?.Trim().ToUpperInvariant())
                    .Where(r => AllRoles.Contains(r))
                    .Distinct()
                    .ToList();
                var title = form?.Title?.Trim();
                var body = form?.Body?.Trim();
                if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body) || roles.Count == 0)
                {
                    return (false, ErrorMessages.AnnouncementInvalid);
                }

                var entity = new Announcement
                {
                    Title = title.Length > 200 ? title.Substring(0, 200) : title,
                    Body = body.Length > 2000 ? body.Substring(0, 2000) : body,
                    TargetRoles = string.Join(",", roles),
                    CreatedByUserId = this._currentUser.GetActorUserId(),
                    CreatedAtUtc = DateTime.UtcNow,
                    // ExpiresOn is the last local day it shows; keep it through the end of that day.
                    ExpiresAtUtc = form.ExpiresOn?.Date.AddDays(1).ToUniversalTime(),
                };
                this.DbContext.Announcements.Add(entity);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("ANNOUNCE", "Announcement", entity.AnnouncementId, null,
                    $"Published announcement '{entity.Title}' to {entity.TargetRoles}.");

                this._notifications.Notify(this._notifications.GetActiveUserIdsInRoles(roles.ToArray()),
                    NotificationTypes.Announcement, entity.Title, entity.Body);

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to publish announcement.");
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) DeleteAnnouncement(long announcementId)
        {
            try
            {
                var entity = this.DbContext.Announcements.FirstOrDefault(a => a.AnnouncementId == announcementId);
                if (entity == null)
                {
                    return (false, ErrorMessages.ServerError);
                }

                this.DbContext.Announcements.Remove(entity);
                this._unitOfWork.SaveChanges();
                this.WriteAudit("DELETE", "Announcement", announcementId, null, $"Deleted announcement '{entity.Title}'.");
                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to delete announcement {AnnouncementId}.", announcementId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public List<AnnouncementRowViewModel> GetActiveAnnouncementsFor(string roleCode, int take = 3)
        {
            if (string.IsNullOrWhiteSpace(roleCode))
            {
                return new List<AnnouncementRowViewModel>();
            }

            var now = DateTime.UtcNow;
            var code = roleCode.ToUpperInvariant();
            // target_roles is a short comma list; match whole codes only.
            return this.DbContext.Announcements.AsNoTracking()
                .Where(a => a.ExpiresAtUtc == null || a.ExpiresAtUtc > now)
                .Where(a => ("," + a.TargetRoles + ",").Contains("," + code + ","))
                .OrderByDescending(a => a.CreatedAtUtc)
                .Take(take)
                .Select(a => new AnnouncementRowViewModel
                {
                    AnnouncementId = a.AnnouncementId,
                    Title = a.Title,
                    Body = a.Body,
                    TargetRoles = a.TargetRoles,
                    CreatedAtUtc = a.CreatedAtUtc,
                    ExpiresAtUtc = a.ExpiresAtUtc,
                    IsActive = true,
                })
                .ToList();
        }

        // ---------------------------------------------------------------- support tickets

        public SupportTicketIndexViewModel GetTickets(string status)
        {
            var isStaff = this.IsStaff();
            var actorId = this._currentUser.GetActorUserId() ?? -1;
            var query = this.DbContext.SupportTickets.AsNoTracking().AsQueryable();
            if (!isStaff)
            {
                query = query.Where(t => t.RequesterUserId == actorId);
            }

            var openCount = query.Count(t => t.Status == SupportTicketStatuses.Open);
            var filter = status?.Trim().ToUpperInvariant();
            if (filter == SupportTicketStatuses.Open || filter == SupportTicketStatuses.Resolved)
            {
                query = query.Where(t => t.Status == filter);
            }

            return new SupportTicketIndexViewModel
            {
                IsStaffView = isStaff,
                Status = filter,
                OpenCount = openCount,
                Items = query
                    .OrderBy(t => t.Status == SupportTicketStatuses.Open ? 0 : 1)
                    .ThenByDescending(t => t.Replies.Max(r => (DateTime?)r.CreatedAtUtc) ?? t.CreatedAtUtc)
                    .Take(100)
                    .Select(t => new SupportTicketRowViewModel
                    {
                        TicketId = t.TicketId,
                        Category = t.Category,
                        Subject = t.Subject,
                        Status = t.Status,
                        RequesterName = t.RequesterUser.FirstName + " " + t.RequesterUser.LastName,
                        RequesterIdentifier = t.RequesterUser.AccountIdentifier,
                        CreatedAtUtc = t.CreatedAtUtc,
                        LastActivityUtc = t.Replies.Max(r => (DateTime?)r.CreatedAtUtc) ?? t.CreatedAtUtc,
                        ReplyCount = t.Replies.Count,
                    })
                    .ToList(),
            };
        }

        public (bool Success, string Error, long TicketId) CreateTicket(SupportTicketFormViewModel form)
        {
            try
            {
                var actorId = this._currentUser.GetActorUserId();
                var category = form?.Category?.Trim().ToUpperInvariant();
                var subject = form?.Subject?.Trim();
                var body = form?.Body?.Trim();
                if (actorId == null || !SupportTicketCategories.All.Contains(category)
                    || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
                {
                    return (false, ErrorMessages.TicketInvalid, 0);
                }

                var ticket = new SupportTicket
                {
                    RequesterUserId = actorId.Value,
                    Category = category,
                    Subject = subject.Length > 200 ? subject.Substring(0, 200) : subject,
                    Body = body.Length > 4000 ? body.Substring(0, 4000) : body,
                    Status = SupportTicketStatuses.Open,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                this.DbContext.SupportTickets.Add(ticket);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("CREATE", "SupportTicket", ticket.TicketId, $"TKT-{ticket.TicketId}",
                    $"Opened {category.ToLowerInvariant()} ticket '{ticket.Subject}'.");

                this._notifications.Notify(this._notifications.GetActiveUserIdsInRoles(RoleCodes.Admin),
                    NotificationTypes.Support,
                    $"New {category.ToLowerInvariant()} ticket",
                    $"{this._currentUser.GetActorName()} opened ticket TKT-{ticket.TicketId}: {ticket.Subject}");

                return (true, null, ticket.TicketId);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to create support ticket.");
                return (false, ErrorMessages.ServerError, 0);
            }
        }

        public SupportTicketDetailsViewModel GetTicket(long ticketId)
        {
            var isStaff = this.IsStaff();
            var actorId = this._currentUser.GetActorUserId() ?? -1;
            var ticket = this.DbContext.SupportTickets.AsNoTracking()
                .Where(t => t.TicketId == ticketId && (isStaff || t.RequesterUserId == actorId))
                .Select(t => new SupportTicketDetailsViewModel
                {
                    TicketId = t.TicketId,
                    Category = t.Category,
                    Subject = t.Subject,
                    Body = t.Body,
                    Status = t.Status,
                    RequesterUserId = t.RequesterUserId,
                    RequesterName = t.RequesterUser.FirstName + " " + t.RequesterUser.LastName,
                    RequesterIdentifier = t.RequesterUser.AccountIdentifier,
                    RequesterStatus = t.RequesterUser.AccountStatus.Description,
                    CreatedAtUtc = t.CreatedAtUtc,
                    ResolvedAtUtc = t.ResolvedAtUtc,
                    Replies = t.Replies
                        .OrderBy(r => r.CreatedAtUtc)
                        .Select(r => new SupportReplyViewModel
                        {
                            AuthorName = r.AuthorUser.FirstName + " " + r.AuthorUser.LastName,
                            AuthorIsStaff = !r.AuthorUser.Role.IsBorrowerRole,
                            Body = r.Body,
                            CreatedAtUtc = r.CreatedAtUtc,
                        })
                        .ToList(),
                })
                .FirstOrDefault();

            if (ticket != null)
            {
                ticket.CanReply = ticket.Status == SupportTicketStatuses.Open;
                ticket.CanResolve = isStaff && ticket.Status == SupportTicketStatuses.Open;
            }

            return ticket;
        }

        public (bool Success, string Error) Reply(long ticketId, string body)
        {
            try
            {
                body = body?.Trim();
                var actorId = this._currentUser.GetActorUserId();
                if (string.IsNullOrWhiteSpace(body) || actorId == null)
                {
                    return (false, ErrorMessages.TicketInvalid);
                }

                var isStaff = this.IsStaff();
                var ticket = this.DbContext.SupportTickets
                    .FirstOrDefault(t => t.TicketId == ticketId && (isStaff || t.RequesterUserId == actorId.Value));
                if (ticket == null)
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                if (ticket.Status != SupportTicketStatuses.Open)
                {
                    return (false, ErrorMessages.TicketClosed);
                }

                this.DbContext.SupportTicketReplies.Add(new SupportTicketReply
                {
                    TicketId = ticket.TicketId,
                    AuthorUserId = actorId.Value,
                    Body = body.Length > 4000 ? body.Substring(0, 4000) : body,
                    CreatedAtUtc = DateTime.UtcNow,
                });
                if (isStaff && ticket.AssignedToUserId == null)
                {
                    ticket.AssignedToUserId = actorId.Value;
                }

                this._unitOfWork.SaveChanges();

                // Staff reply → requester; requester reply → the staff member handling it (or all admins).
                var recipients = isStaff
                    ? new List<long> { ticket.RequesterUserId }
                    : ticket.AssignedToUserId.HasValue
                        ? new List<long> { ticket.AssignedToUserId.Value }
                        : this._notifications.GetActiveUserIdsInRoles(RoleCodes.Admin);
                this._notifications.Notify(recipients, NotificationTypes.Support,
                    $"New reply on TKT-{ticket.TicketId}",
                    $"{this._currentUser.GetActorName()} replied to \"{ticket.Subject}\".");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to reply to ticket {TicketId}.", ticketId);
                return (false, ErrorMessages.ServerError);
            }
        }

        public (bool Success, string Error) Resolve(long ticketId)
        {
            try
            {
                if (!this.IsStaff())
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                var ticket = this.DbContext.SupportTickets.FirstOrDefault(t => t.TicketId == ticketId);
                if (ticket == null)
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                if (ticket.Status == SupportTicketStatuses.Resolved)
                {
                    return (true, null);
                }

                ticket.Status = SupportTicketStatuses.Resolved;
                ticket.ResolvedAtUtc = DateTime.UtcNow;
                ticket.AssignedToUserId ??= this._currentUser.GetActorUserId();
                this._unitOfWork.SaveChanges();

                this.WriteAudit("RESOLVE", "SupportTicket", ticket.TicketId, $"TKT-{ticket.TicketId}",
                    $"Resolved ticket '{ticket.Subject}'.");
                this._notifications.Notify(ticket.RequesterUserId, NotificationTypes.Support,
                    $"Ticket TKT-{ticket.TicketId} resolved",
                    $"Your ticket \"{ticket.Subject}\" was marked resolved. Open a new ticket if you still need help.");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to resolve ticket {TicketId}.", ticketId);
                return (false, ErrorMessages.ServerError);
            }
        }

        // ---------------------------------------------------------------- reports

        public VerificationStatsViewModel GetVerificationStats()
        {
            var counts = this.DbContext.Users.AsNoTracking()
                .GroupBy(u => u.AccountStatus.Code)
                .Select(g => new { Code = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Code, x => x.Count);

            var durations = this.DbContext.Users.AsNoTracking()
                .Where(u => u.VerifiedAtUtc != null && u.CreatedAtUtc != default)
                .Select(u => new { u.CreatedAtUtc, Verified = u.VerifiedAtUtc.Value })
                .ToList()
                .Select(x => (x.Verified - x.CreatedAtUtc).TotalHours)
                .Where(h => h >= 0)
                .OrderBy(h => h)
                .ToList();

            double? median = null;
            if (durations.Count > 0)
            {
                var mid = durations.Count / 2;
                median = durations.Count % 2 == 1 ? durations[mid] : (durations[mid - 1] + durations[mid]) / 2;
            }

            return new VerificationStatsViewModel
            {
                Pending = counts.TryGetValue(AccountStatusCodes.Unverified, out var p) ? p : 0,
                Rejected = counts.TryGetValue(AccountStatusCodes.Rejected, out var r) ? r : 0,
                Approved = this.DbContext.AuditLogs.Count(a => a.ActionType == "VERIFY"),
                MedianHoursToDecision = median,
            };
        }

        public BorrowerHistoryViewModel GetBorrowerHistory(long userId)
        {
            var user = this.DbContext.Users.AsNoTracking()
                .Where(u => u.UserId == userId)
                .Select(u => new { u.UserId, Name = u.FirstName + " " + u.LastName, u.AccountIdentifier })
                .FirstOrDefault();
            if (user == null)
            {
                return null;
            }

            return new BorrowerHistoryViewModel
            {
                UserId = user.UserId,
                BorrowerName = user.Name,
                AccountIdentifier = user.AccountIdentifier,
                Reservations = this.DbContext.Reservations.AsNoTracking()
                    .Where(r => r.BorrowerUserId == userId)
                    .OrderByDescending(r => r.SubmittedAtUtc)
                    .Select(r => new BorrowerHistoryRowViewModel
                    {
                        Number = r.ReservationNumber,
                        EquipmentName = r.Equipment.EquipmentName,
                        AssetCode = r.Equipment.AssetCode,
                        Quantity = r.RequestedQuantity,
                        Status = r.Status.Description,
                        StartUtc = r.RequestedStartAtUtc,
                        EndUtc = r.ExpectedReturnAtUtc,
                    })
                    .ToList(),
                Borrowings = this.DbContext.Borrowings.AsNoTracking()
                    .Where(b => b.BorrowerUserId == userId)
                    .OrderByDescending(b => b.ReleasedAtUtc)
                    .Select(b => new BorrowerHistoryRowViewModel
                    {
                        Number = b.TransactionNumber,
                        EquipmentName = b.Equipment.EquipmentName,
                        AssetCode = b.Equipment.AssetCode,
                        Quantity = b.ReleasedQuantity,
                        Status = b.Status.Description,
                        StartUtc = b.ReleasedAtUtc,
                        EndUtc = b.DueAtUtc,
                        Late = b.Status.Code == "OVERDUE" || b.BorrowingReturns.Any(x => x.WasReturnedLate),
                    })
                    .ToList(),
            };
        }

        // ---------------------------------------------------------------- helpers

        private bool IsStaff()
        {
            var role = this._currentUser.GetActorRoleCode();
            return string.Equals(role, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase);
        }

        private static AnnouncementRowViewModel WithAudienceLabel(AnnouncementRowViewModel row)
        {
            var labels = (row.TargetRoles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(code => code switch
                {
                    RoleCodes.Student => "Students",
                    RoleCodes.Faculty => "Faculty",
                    RoleCodes.Technician => "Custodians",
                    RoleCodes.Admin => "Administrators",
                    _ => code,
                });
            row.AudienceLabel = string.Join(", ", labels);
            return row;
        }

        private void WriteAudit(string actionType, string entityType, long entityId, string referenceNumber, string details)
        {
            this.DbContext.AuditLogs.Add(new AuditLog
            {
                ActorUserId = this._currentUser.GetActorUserId(),
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                ReferenceNumber = referenceNumber,
                Details = details,
                OccurredAtUtc = DateTime.UtcNow,
            });
            this._unitOfWork.SaveChanges();
        }
    }
}
