using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Models;

public partial class Notification
{
    public long NotificationId { get; set; }

    public long RecipientUserId { get; set; }

    public string NotificationType { get; set; }

    /// <summary>Local URL the notification opens; null for legacy rows.</summary>
    public string LinkUrl { get; set; }

    public string Title { get; set; }

    public string Message { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public virtual User RecipientUser { get; set; }
}
