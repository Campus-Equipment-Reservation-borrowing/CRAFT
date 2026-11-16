using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Borrowing queries shared by services. Status codes are passed in by the caller.
    /// </summary>
    public interface IBorrowingRepository
    {
        /// <summary>Borrowings of the borrower whose status code is the given one.</summary>
        int CountForBorrowerWithStatus(long borrowerUserId, string statusCode);

        /// <summary>Borrowings of the borrower whose status code is neither of the given ones.</summary>
        int CountForBorrowerWithoutStatuses(long borrowerUserId, string firstStatusCode, string secondStatusCode);

        /// <summary>Borrowings of the borrower in one of the given statuses and due before the cutoff.</summary>
        int CountForBorrowerInStatusesDueBefore(long borrowerUserId, IReadOnlyCollection<string> statusCodes, DateTime dueBeforeUtc);
    }
}
