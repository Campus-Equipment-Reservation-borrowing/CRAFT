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

        public bool BorrowerHasAssignmentWithCustodianStatus(long borrowerUserId, string custodianAccountStatusCode)
        {
            return this.Context.CustodianAssignments.Any(a => a.BorrowerUserId == borrowerUserId
                && a.CustodianUser.AccountStatus.Code == custodianAccountStatusCode);
        }

        private IQueryable<CustodianAssignment> WithDetails()
        {
            return this.Context.CustodianAssignments.AsNoTracking()
                .Include(a => a.CustodianUser)
                .Include(a => a.BorrowerUser)
                .Include(a => a.Location)
                .Include(a => a.EquipmentCategory);
        }

        public List<CustodianAssignment> GetAllWithDetails()
        {
            return this.WithDetails().ToList();
        }

        public CustodianAssignment GetWithDetailsById(long assignmentId)
        {
            return this.WithDetails().FirstOrDefault(a => a.AssignmentId == assignmentId);
        }

        public List<CustodianAssignment> GetWithDetailsForCustodian(long custodianUserId)
        {
            return this.WithDetails().Where(a => a.CustodianUserId == custodianUserId).ToList();
        }

        public CustodianAssignment GetTrackedById(long assignmentId)
        {
            return this.Context.CustodianAssignments.FirstOrDefault(a => a.AssignmentId == assignmentId);
        }

        public CustodianAssignment GetTrackedForBorrower(long borrowerUserId)
        {
            return this.Context.CustodianAssignments.FirstOrDefault(a => a.BorrowerUserId == borrowerUserId);
        }

        public CustodianAssignment GetForBorrowerWithCustodian(long borrowerUserId)
        {
            return this.Context.CustodianAssignments.AsNoTracking()
                .Include(a => a.CustodianUser)
                .FirstOrDefault(a => a.BorrowerUserId == borrowerUserId);
        }

        public bool LocationHasAssignment(long locationId)
        {
            return this.Context.CustodianAssignments.Any(a => a.LocationId == locationId);
        }

        public List<(string ScopeType, long CustodianUserId)> GetRoutingCandidates(long borrowerUserId, long? locationId, string custodianAccountStatusCode)
        {
            return this.Context.CustodianAssignments.AsNoTracking()
                .Where(a => a.CustodianUser.AccountStatus.Code == custodianAccountStatusCode
                    && (a.BorrowerUserId == borrowerUserId
                        || (locationId != null && a.LocationId == locationId)))
                .Select(a => new { a.ScopeType, a.CustodianUserId })
                .ToList()
                .Select(a => (a.ScopeType, a.CustodianUserId))
                .ToList();
        }

        public Dictionary<long, int> CountByCustodian()
        {
            return this.Context.CustodianAssignments.AsNoTracking()
                .GroupBy(a => a.CustodianUserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionary(x => x.Key, x => x.Count);
        }

        public void Add(CustodianAssignment assignment)
        {
            this.Context.CustodianAssignments.Add(assignment);
        }

        public void Remove(CustodianAssignment assignment)
        {
            this.Context.CustodianAssignments.Remove(assignment);
        }
    }
}
