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

        public AnnouncementIndexViewModel GetAnnouncements(int page = 1, int pageSize = 10)
        {
            const int DefaultPageSize = 10;
            page = page < 1 ? 1 : page;
            pageSize = pageSize < 1 ? DefaultPageSize : pageSize;

            var now = DateTime.UtcNow;
            var query = this.DbContext.Announcements.AsNoTracking();
            var totalCount = query.Count();

            var items = query
                .OrderByDescending(a => a.CreatedAtUtc)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AnnouncementRowViewModel
                {
                    AnnouncementId = a.AnnouncementId,
                    Title = a.Title,
                    Body = a.Body,
                    TargetRoles = a.TargetRoles,
                    Category = a.Category,
                    CreatedByName = a.CreatedByUser == null ? null : a.CreatedByUser.FirstName + " " + a.CreatedByUser.LastName,
                    CreatedAtUtc = a.CreatedAtUtc,
                    ExpiresAtUtc = a.ExpiresAtUtc,
                    IsActive = a.ExpiresAtUtc == null || a.ExpiresAtUtc > now,
                })
                .ToList()
                .Select(WithAudienceLabel)
                .ToList();

            return new AnnouncementIndexViewModel
            {
                Items = items,
                Page = new PagedResult<AnnouncementRowViewModel>
                {
                    Items = items,
                    Page = page,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                },
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

                var category = (form.Category ?? "GENERAL").Trim().ToUpperInvariant();
                if (category != "MAINTENANCE" && category != "RULE_CHANGE" && category != "GENERAL")
                {
                    category = "GENERAL";
                }

                var entity = new Announcement
                {
                    Title = title.Length > 200 ? title.Substring(0, 200) : title,
                    Body = body.Length > 2000 ? body.Substring(0, 2000) : body,
                    TargetRoles = string.Join(",", roles),
                    Category = category,
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
            // Only maintenance notices stay as dashboard banners; everything else is delivered
            // through the Notifications page.
            return this.DbContext.Announcements.AsNoTracking()
                .Where(a => a.ExpiresAtUtc == null || a.ExpiresAtUtc > now)
                .Where(a => a.Category == "MAINTENANCE")
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

        public AnnouncementRowViewModel GetActiveMaintenanceAnnouncement()
        {
            var now = DateTime.UtcNow;
            return this.DbContext.Announcements.AsNoTracking()
                .Where(a => a.Category == "MAINTENANCE")
                .Where(a => a.ExpiresAtUtc == null || a.ExpiresAtUtc > now)
                .OrderByDescending(a => a.CreatedAtUtc)
                .Select(a => new AnnouncementRowViewModel
                {
                    AnnouncementId = a.AnnouncementId,
                    Title = a.Title,
                    Body = a.Body,
                    Category = a.Category,
                    CreatedAtUtc = a.CreatedAtUtc,
                    ExpiresAtUtc = a.ExpiresAtUtc,
                    IsActive = true,
                })
                .FirstOrDefault();
        }

        // ---------------------------------------------------------------- support tickets

        public SupportTicketIndexViewModel GetTickets(string status, string view = null)
        {
            var role = this._currentUser.GetActorRoleCode();
            var actorId = this._currentUser.GetActorUserId() ?? -1;
            var isBorrower = string.Equals(role, RoleCodes.Student, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, RoleCodes.Faculty, StringComparison.OrdinalIgnoreCase);
            var isAdmin = string.Equals(role, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase);
            var isTechnician = string.Equals(role, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase);

            var query = this.DbContext.SupportTickets.AsNoTracking().AsQueryable();
            if (isBorrower)
            {
                query = query.Where(t => t.RequesterUserId == actorId);
            }
            else if (isAdmin)
            {
                // Admins only handle tickets a custodian forwarded.
                query = query.Where(t => t.TargetRoleCode == RoleCodes.Admin);
            }
            else if (isTechnician)
            {
                if (string.Equals(view, "mine", StringComparison.OrdinalIgnoreCase))
                {
                    // Tickets the custodian raised for the administrator.
                    query = query.Where(t => t.RequesterUserId == actorId);
                }
                else if (string.Equals(view, "forwarded", StringComparison.OrdinalIgnoreCase))
                {
                    // Borrower tickets this custodian escalated.
                    query = query.Where(t => t.EscalatedByUserId == actorId);
                }
                else
                {
                    query = query.Where(t => t.TargetRoleCode == RoleCodes.Technician);
                }
            }

            var openCount = query.Count(t => t.Status != SupportTicketStatuses.Resolved);
            var filter = status?.Trim().ToUpperInvariant();
            if (filter == "OPEN")
            {
                query = query.Where(t => t.Status != SupportTicketStatuses.Resolved);
            }
            else if (filter == SupportTicketStatuses.Resolved)
            {
                query = query.Where(t => t.Status == SupportTicketStatuses.Resolved);
            }

            var items = query
                .OrderBy(t => t.Status == SupportTicketStatuses.Resolved ? 1 : 0)
                .ThenByDescending(t => t.Replies.Max(r => (DateTime?)r.CreatedAtUtc) ?? t.CreatedAtUtc)
                .Select(t => new SupportTicketRowViewModel
                {
                    TicketId = t.TicketId,
                    Category = t.Category,
                    Subject = t.Subject,
                    Status = t.Status,
                    TargetRoleCode = t.TargetRoleCode,
                    RequesterName = t.RequesterUser.FirstName + " " + t.RequesterUser.LastName,
                    RequesterIdentifier = t.RequesterUser.AccountIdentifier,
                    CreatedAtUtc = t.CreatedAtUtc,
                    LastActivityUtc = t.Replies.Max(r => (DateTime?)r.CreatedAtUtc) ?? t.CreatedAtUtc,
                    ReplyCount = t.Replies.Count,
                })
                .Take(100)
                .ToList();

            return new SupportTicketIndexViewModel
            {
                IsStaffView = isAdmin || isTechnician,
                IsCustodian = isTechnician,
                IsAdmin = isAdmin,
                View = string.Equals(view, "mine", StringComparison.OrdinalIgnoreCase) ? "mine"
                    : string.Equals(view, "forwarded", StringComparison.OrdinalIgnoreCase) ? "forwarded"
                    : "inbox",
                Status = filter,
                OpenCount = openCount,
                Items = items,
            };
        }

        public (bool Success, string Error, long TicketId) CreateTicket(SupportTicketFormViewModel form)
        {
            try
            {
                var actorId = this._currentUser.GetActorUserId();
                var actorRole = this._currentUser.GetActorRoleCode();
                var category = form?.Category?.Trim().ToUpperInvariant();
                var subject = form?.Subject?.Trim();
                var body = form?.Body?.Trim();
                if (actorId == null || !SupportTicketCategories.All.Contains(category)
                    || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
                {
                    return (false, ErrorMessages.TicketInvalid, 0);
                }

                // Borrowers raise tickets for custodians; custodians raise/forward to admins.
                // Administrators resolve tickets and cannot create their own.
                string targetRole;
                if (string.Equals(actorRole, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase))
                {
                    targetRole = RoleCodes.Admin;
                }
                else if (string.Equals(actorRole, RoleCodes.Student, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(actorRole, RoleCodes.Faculty, StringComparison.OrdinalIgnoreCase))
                {
                    targetRole = RoleCodes.Technician;
                }
                else
                {
                    return (false, "Administrators cannot create support tickets.", 0);
                }

                var ticket = new SupportTicket
                {
                    RequesterUserId = actorId.Value,
                    Category = category,
                    Subject = subject.Length > 200 ? subject.Substring(0, 200) : subject,
                    Body = body.Length > 4000 ? body.Substring(0, 4000) : body,
                    Status = SupportTicketStatuses.Open,
                    TargetRoleCode = targetRole,
                    CreatedAtUtc = DateTime.UtcNow,
                };
                this.DbContext.SupportTickets.Add(ticket);
                this._unitOfWork.SaveChanges();

                this.WriteAudit("CREATE", "SupportTicket", ticket.TicketId, $"TKT-{ticket.TicketId}",
                    $"Opened {category.ToLowerInvariant()} ticket '{ticket.Subject}'.");

                this._notifications.Notify(this._notifications.GetActiveUserIdsInRoles(targetRole),
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
            var role = this._currentUser.GetActorRoleCode();
            var actorId = this._currentUser.GetActorUserId() ?? -1;
            var isBorrower = string.Equals(role, RoleCodes.Student, StringComparison.OrdinalIgnoreCase)
                || string.Equals(role, RoleCodes.Faculty, StringComparison.OrdinalIgnoreCase);
            var isAdmin = string.Equals(role, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase);

            var query = this.DbContext.SupportTickets.AsNoTracking().Where(t => t.TicketId == ticketId);
            if (isBorrower)
            {
                query = query.Where(t => t.RequesterUserId == actorId);
            }
            else if (isAdmin)
            {
                query = query.Where(t => t.TargetRoleCode == RoleCodes.Admin);
            }
            else if (isStaff)
            {
                query = query.Where(t => t.TargetRoleCode == RoleCodes.Technician
                    || t.RequesterUserId == actorId || t.EscalatedByUserId == actorId);
            }

            var ticket = query
                .Select(t => new SupportTicketDetailsViewModel
                {
                    TicketId = t.TicketId,
                    Category = t.Category,
                    Subject = t.Subject,
                    Body = t.Body,
                    Status = t.Status,
                    TargetRoleCode = t.TargetRoleCode,
                    RequesterUserId = t.RequesterUserId,
                    RequesterName = t.RequesterUser.FirstName + " " + t.RequesterUser.LastName,
                    RequesterIdentifier = t.RequesterUser.AccountIdentifier,
                    RequesterStatus = t.RequesterUser.AccountStatus.Description,
                    CreatedAtUtc = t.CreatedAtUtc,
                    ResolvedAtUtc = t.ResolvedAtUtc,
                    EscalatedAtUtc = t.EscalatedAtUtc,
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
                var isTechnician = string.Equals(role, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase);
                var isOwnTicket = ticket.RequesterUserId == actorId;
                ticket.CanResolve = ticket.Status != SupportTicketStatuses.Resolved
                    && ((isAdmin && ticket.TargetRoleCode == RoleCodes.Admin)
                        || (isTechnician && ticket.TargetRoleCode == RoleCodes.Technician && !isOwnTicket));

                // Only the receiving staff replies; the creator can read but not post follow-ups.
                ticket.CanReply = ticket.CanResolve;
                ticket.CanEscalate = isTechnician
                    && ticket.TargetRoleCode == RoleCodes.Technician
                    && !isOwnTicket
                    && ticket.Status != SupportTicketStatuses.Resolved;
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
                var role = this._currentUser.GetActorRoleCode();
                var isAdmin = string.Equals(role, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase);
                var isTechnician = string.Equals(role, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase);

                var ticketQuery = this.DbContext.SupportTickets.Where(t => t.TicketId == ticketId);
                if (isAdmin)
                {
                    ticketQuery = ticketQuery.Where(t => t.TargetRoleCode == RoleCodes.Admin);
                }
                else if (isTechnician)
                {
                    ticketQuery = ticketQuery.Where(t => t.TargetRoleCode == RoleCodes.Technician
                        || t.RequesterUserId == actorId.Value || t.EscalatedByUserId == actorId.Value);
                }
                else
                {
                    ticketQuery = ticketQuery.Where(t => t.RequesterUserId == actorId.Value);
                }

                var ticket = ticketQuery.FirstOrDefault();
                if (ticket == null)
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                if (ticket.Status == SupportTicketStatuses.Resolved)
                {
                    return (false, ErrorMessages.TicketClosed);
                }

                // The person who opened a ticket cannot reply to it; only the receiving staff can.
                if (ticket.RequesterUserId == actorId.Value)
                {
                    return (false, "You cannot reply to your own ticket. Please create a new ticket if you need more help.");
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

                // Staff reply → requester; requester reply → the handler, else the target role queue.
                var recipients = isStaff
                    ? new List<long> { ticket.RequesterUserId }
                    : ticket.AssignedToUserId.HasValue
                        ? new List<long> { ticket.AssignedToUserId.Value }
                        : this._notifications.GetActiveUserIdsInRoles(ticket.TargetRoleCode ?? RoleCodes.Technician);
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

                var role = this._currentUser.GetActorRoleCode();
                var ticket = this.DbContext.SupportTickets.FirstOrDefault(t => t.TicketId == ticketId);
                if (ticket == null)
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                // Custodians resolve borrower tickets; administrators resolve forwarded ones.
                var canResolve = string.Equals(role, RoleCodes.Admin, StringComparison.OrdinalIgnoreCase)
                    ? ticket.TargetRoleCode == RoleCodes.Admin
                    : string.Equals(role, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase)
                        && ticket.TargetRoleCode == RoleCodes.Technician;
                if (!canResolve)
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

        public (bool Success, string Error) Escalate(long ticketId)
        {
            try
            {
                var role = this._currentUser.GetActorRoleCode();
                if (!string.Equals(role, RoleCodes.Technician, StringComparison.OrdinalIgnoreCase))
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                var actorId = this._currentUser.GetActorUserId();
                var ticket = this.DbContext.SupportTickets.FirstOrDefault(t => t.TicketId == ticketId);
                if (ticket == null)
                {
                    return (false, ErrorMessages.TicketNotFound);
                }

                if (ticket.Status == SupportTicketStatuses.Resolved
                    || ticket.TargetRoleCode == RoleCodes.Admin)
                {
                    return (false, "This ticket has already been forwarded or resolved.");
                }

                ticket.TargetRoleCode = RoleCodes.Admin;
                ticket.Status = SupportTicketStatuses.Escalated;
                ticket.EscalatedAtUtc = DateTime.UtcNow;
                ticket.EscalatedByUserId = actorId;
                this._unitOfWork.SaveChanges();

                this.WriteAudit("ESCALATE", "SupportTicket", ticket.TicketId, $"TKT-{ticket.TicketId}",
                    $"Forwarded ticket '{ticket.Subject}' to the administrator.");
                this._notifications.Notify(this._notifications.GetActiveUserIdsInRoles(RoleCodes.Admin),
                    NotificationTypes.Support,
                    $"Ticket TKT-{ticket.TicketId} forwarded",
                    $"{this._currentUser.GetActorName()} forwarded \"{ticket.Subject}\" for administrator review.");
                this._notifications.Notify(ticket.RequesterUserId, NotificationTypes.Support,
                    $"Ticket TKT-{ticket.TicketId} forwarded",
                    "Your ticket was forwarded to the administrator for further review.");

                return (true, null);
            }
            catch (Exception ex)
            {
                this._logger.LogError(ex, "Failed to escalate ticket {TicketId}.", ticketId);
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
