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

        /// <summary>True when the borrower has an assignment whose custodian has the given account status.</summary>
        bool BorrowerHasAssignmentWithCustodianStatus(long borrowerUserId, string custodianAccountStatusCode);

        /// <summary>Every assignment (untracked) with custodian, borrower, location and category.</summary>
        List<CustodianAssignment> GetAllWithDetails();

        /// <summary>The assignment (untracked) with custodian, borrower, location and category, or null.</summary>
        CustodianAssignment GetWithDetailsById(long assignmentId);

        /// <summary>Assignments of the custodian (untracked) with custodian, borrower, location and category.</summary>
        List<CustodianAssignment> GetWithDetailsForCustodian(long custodianUserId);

        CustodianAssignment GetTrackedById(long assignmentId);

        /// <summary>The borrower-scoped assignment of the borrower (tracked), or null.</summary>
        CustodianAssignment GetTrackedForBorrower(long borrowerUserId);

        /// <summary>The borrower-scoped assignment of the borrower (untracked, with custodian), or null.</summary>
        CustodianAssignment GetForBorrowerWithCustodian(long borrowerUserId);

        bool LocationHasAssignment(long locationId);

        /// <summary>Scope type and custodian of assignments matching the borrower or location whose custodian has the account status.</summary>
        List<(string ScopeType, long CustodianUserId)> GetRoutingCandidates(long borrowerUserId, long? locationId, string custodianAccountStatusCode);

        /// <summary>Number of assignments per custodian user id.</summary>
        Dictionary<long, int> CountByCustodian();

        /// <summary>Adds an assignment (saved by the caller).</summary>
        void Add(CustodianAssignment assignment);

        /// <summary>Removes an assignment (saved by the caller).</summary>
        void Remove(CustodianAssignment assignment);
    }
}
