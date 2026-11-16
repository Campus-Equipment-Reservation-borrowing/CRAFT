using ASI.Basecode.Data.Models;
using ASI.Basecode.Data.QueryModels;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    public interface IReturnProcessingRepository
    {
        Borrowing GetBorrowingForReturn(long borrowingId);

        PagedResult<BorrowingReturn> GetReturnsPaged(string search, int page, int pageSize, long? scopeCustodianUserId = null);

        BorrowingReturn GetReturnWithDetails(long returnId);

        /// <summary>Of the given borrowings, those whose returned units exceed the units covered by inspections in the given status.</summary>
        HashSet<long> GetBorrowingIdsWithUninspectedReturns(IReadOnlyCollection<long> borrowingIds, string completedInspectionStatusCode);

        /// <summary>Units of a borrowing covered by inspections in the status, optionally excluding one inspection.</summary>
        int SumInspectedQuantity(long borrowingId, string inspectionStatusCode, long? excludeInspectionId = null);

        /// <summary>Lost units of a borrowing across inspections in the status, excluding one inspection.</summary>
        int SumLostQuantity(long borrowingId, long excludeInspectionId, string inspectionStatusCode);

        /// <summary>Inspection ids of the borrowing in ascending order.</summary>
        List<long> GetInspectionIds(long borrowingId);

        /// <summary>Whether the inspection exists and, if so, the custodian assigned to its borrowing's reservation.</summary>
        (bool Exists, long? AssignedCustodianUserId) GetInspectionCustodian(long inspectionId);

        /// <summary>Adds a return (saved by the caller).</summary>
        void AddReturn(BorrowingReturn borrowingReturn);

        /// <summary>Adds an inspection (saved by the caller).</summary>
        void AddInspection(ConditionInspection inspection);
    }
}
