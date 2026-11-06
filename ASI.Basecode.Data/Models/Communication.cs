using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

/// <summary>Admin broadcast to one or more roles. Tables created by <c>DatabaseSeeder</c>.</summary>
public partial class Announcement
{
    public long AnnouncementId { get; set; }

    public string Title { get; set; }

    public string Body { get; set; }

    /// <summary>Comma-separated role codes, or ALL.</summary>
    public string TargetRoles { get; set; }

    public long? CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ExpiresAtUtc { get; set; }

    public virtual User CreatedByUser { get; set; }
}

/// <summary>Borrower → staff support request, verification appeal or feedback.</summary>
public partial class SupportTicket
{
    public long TicketId { get; set; }

    public long RequesterUserId { get; set; }

    /// <summary>VERIFICATION | APPEAL | FEEDBACK | OTHER.</summary>
    public string Category { get; set; }

    public string Subject { get; set; }

    public string Body { get; set; }

    /// <summary>OPEN | RESOLVED.</summary>
    public string Status { get; set; }

    public long? AssignedToUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? ResolvedAtUtc { get; set; }

    public virtual User RequesterUser { get; set; }

    public virtual User AssignedToUser { get; set; }

    public virtual ICollection<SupportTicketReply> Replies { get; set; } = new List<SupportTicketReply>();
}

public partial class SupportTicketReply
{
    public long ReplyId { get; set; }

    public long TicketId { get; set; }

    public long AuthorUserId { get; set; }

    public string Body { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual SupportTicket Ticket { get; set; }

    public virtual User AuthorUser { get; set; }
}
