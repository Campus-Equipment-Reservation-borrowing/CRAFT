using System;

namespace ASI.Basecode.Data.Models;

/// <summary>
/// Account lifecycle columns (verification, provisioning, lockout) added on top of the
/// scaffolded users table by <c>DatabaseSeeder</c>. Separate partial so re-scaffolding never clobbers it.
/// </summary>
public partial class User
{
    /// <summary>Reason given when an administrator rejected the account's verification.</summary>
    public string VerificationNote { get; set; }

    public DateTime? VerifiedAtUtc { get; set; }

    public long? VerifiedByUserId { get; set; }

    /// <summary>Set for admin-provisioned or admin-reset passwords; cleared on the first password change.</summary>
    public bool MustChangePassword { get; set; }

    /// <summary>Consecutive failed sign-ins since the last success or lockout.</summary>
    public int FailedLoginCount { get; set; }

    /// <summary>Sign-in is refused until this instant after too many failed attempts.</summary>
    public DateTime? LockoutUntilUtc { get; set; }
}
