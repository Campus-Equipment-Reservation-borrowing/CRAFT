using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Custodian assignment repository implementation. Data access only.
    /// </summary>
    public class CustodianAssignmentRepository : BaseRepository, ICustodianAssignmentRepository
    {
        public CustodianAssignmentRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public Dictionary<long, long> GetCustodianByBorrower(IReadOnlyCollection<long> borrowerUserIds)
        {
            return this.Context.CustodianAssignments.AsNoTracking()
                .Where(a => a.BorrowerUserId != null && borrowerUserIds.Contains(a.BorrowerUserId.Value))
                .Select(a => new { BorrowerId = a.BorrowerUserId.Value, a.CustodianUserId })
                .ToDictionary(a => a.BorrowerId, a => a.CustodianUserId);
        }

        public bool BorrowerHasAssignment(long borrowerUserId)
        {
            return this.Context.CustodianAssignments.Any(a => a.BorrowerUserId == borrowerUserId);
        }

        public bool CustodianHasAssignment(long custodianUserId)
        {
            return this.Context.CustodianAssignments.Any(a => a.CustodianUserId == custodianUserId);
        }

        public List<CustodianAssignment> GetTrackedInvolvingUser(long userId)
        {
            return this.Context.CustodianAssignments
                .Where(a => a.CustodianUserId == userId || a.BorrowerUserId == userId)
                .ToList();
        }

        public void RemoveRange(IEnumerable<CustodianAssignment> assignments)
        {
            this.Context.CustodianAssignments.RemoveRange(assignments);
        }
    }
}
