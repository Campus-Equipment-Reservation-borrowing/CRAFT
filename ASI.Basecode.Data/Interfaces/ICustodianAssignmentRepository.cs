using ASI.Basecode.Data.Models;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for custodian assignments.
    /// </summary>
    public interface ICustodianAssignmentRepository
    {
        /// <summary>Custodian user id keyed by borrower id, for borrower-scoped assignments of the given borrowers.</summary>
        Dictionary<long, long> GetCustodianByBorrower(IReadOnlyCollection<long> borrowerUserIds);

        /// <summary>True when a borrower-scoped assignment exists for the borrower.</summary>
        bool BorrowerHasAssignment(long borrowerUserId);

        /// <summary>True when the user is the custodian on any assignment.</summary>
        bool CustodianHasAssignment(long custodianUserId);

        /// <summary>Assignments where the user is the custodian or the borrower (tracked).</summary>
        List<CustodianAssignment> GetTrackedInvolvingUser(long userId);

        /// <summary>Removes the assignments (saved by the caller).</summary>
        void RemoveRange(IEnumerable<CustodianAssignment> assignments);
    }
}
