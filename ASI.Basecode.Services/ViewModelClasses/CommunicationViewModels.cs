using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ASI.Basecode.Services.ViewModelClasses
{
    public class AnnouncementRowViewModel
    {
        public long AnnouncementId { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string TargetRoles { get; set; }
        public string AudienceLabel { get; set; }
        public string CreatedByName { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
        public bool IsActive { get; set; }
    }

    public class AnnouncementFormViewModel
    {
        [Required(ErrorMessage = "Title is required.")]
        [StringLength(200)]
        public string Title { get; set; }

        [Required(ErrorMessage = "Message is required.")]
        [StringLength(2000)]
        public string Body { get; set; }

        /// <summary>Role codes to reach (STUDENT, FACULTY, TECHNICIAN, ADMIN); empty means nobody.</summary>
        public List<string> TargetRoles { get; set; } = new();

        /// <summary>Optional local date after which the banner stops showing.</summary>
        public DateTime? ExpiresOn { get; set; }
    }

    public class AnnouncementIndexViewModel
    {
        public List<AnnouncementRowViewModel> Items { get; set; } = new();
        public AnnouncementFormViewModel Form { get; set; } = new();
    }

    public class SupportTicketRowViewModel
    {
        public long TicketId { get; set; }
        public string Category { get; set; }
        public string Subject { get; set; }
        public string Status { get; set; }
        public string RequesterName { get; set; }
        public string RequesterIdentifier { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime LastActivityUtc { get; set; }
        public int ReplyCount { get; set; }
    }

    public class SupportTicketIndexViewModel
    {
        public List<SupportTicketRowViewModel> Items { get; set; } = new();
        /// <summary>Staff see every ticket; borrowers see their own.</summary>
        public bool IsStaffView { get; set; }
        public string Status { get; set; }
        public int OpenCount { get; set; }
    }

    public class SupportTicketFormViewModel
    {
        [Required(ErrorMessage = "Choose a category.")]
        public string Category { get; set; }

        [Required(ErrorMessage = "Subject is required.")]
        [StringLength(200)]
        public string Subject { get; set; }

        [Required(ErrorMessage = "Message is required.")]
        [StringLength(4000)]
        public string Body { get; set; }
    }

    public class SupportReplyViewModel
    {
        public string AuthorName { get; set; }
        public bool AuthorIsStaff { get; set; }
        public string Body { get; set; }
        public DateTime CreatedAtUtc { get; set; }
    }

    public class SupportTicketDetailsViewModel
    {
        public long TicketId { get; set; }
        public string Category { get; set; }
        public string Subject { get; set; }
        public string Body { get; set; }
        public string Status { get; set; }
        public long RequesterUserId { get; set; }
        public string RequesterName { get; set; }
        public string RequesterIdentifier { get; set; }
        public string RequesterStatus { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? ResolvedAtUtc { get; set; }
        public List<SupportReplyViewModel> Replies { get; set; } = new();
        public bool CanResolve { get; set; }
        public bool CanReply { get; set; }
    }

    /// <summary>Admin: one borrower's reservation and borrowing history.</summary>
    public class BorrowerHistoryViewModel
    {
        public long UserId { get; set; }
        public string BorrowerName { get; set; }
        public string AccountIdentifier { get; set; }
        public List<BorrowerHistoryRowViewModel> Reservations { get; set; } = new();
        public List<BorrowerHistoryRowViewModel> Borrowings { get; set; } = new();
    }

    public class BorrowerHistoryRowViewModel
    {
        public string Number { get; set; }
        public string EquipmentName { get; set; }
        public string AssetCode { get; set; }
        public int Quantity { get; set; }
        public string Status { get; set; }
        public DateTime StartUtc { get; set; }
        public DateTime EndUtc { get; set; }
        public bool Late { get; set; }
    }

    /// <summary>Monitoring dashboard: verification pipeline health.</summary>
    public class VerificationStatsViewModel
    {
        public int Pending { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public double? MedianHoursToDecision { get; set; }
    }
}
