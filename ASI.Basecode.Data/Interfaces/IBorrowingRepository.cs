using ASI.Basecode.Data.Models;
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
    
        /// <summary>Borrowings of the borrower not in either excluded status, optionally searched, by due date (untracked, with equipment/status/release condition).</summary>
        List<Borrowing> GetForBorrowerExcludingStatuses(long borrowerUserId, string firstExcludedStatusCode, string secondExcludedStatusCode, string search);

        /// <summary>One borrowing of the borrower with full detail navigations (untracked), or null.</summary>
        Borrowing GetDetailsForBorrower(long borrowingId, long borrowerUserId);

        /// <summary>Borrowings of the borrower in either status, optionally searched, latest return first (untracked).</summary>
        List<Borrowing> GetHistoryForBorrower(long borrowerUserId, string firstStatusCode, string secondStatusCode, string search);

        /// <summary>Tracked borrowings in one of the statuses and due before the cutoff, with equipment and reservation.</summary>
        List<Borrowing> GetTrackedInStatusesDueBefore(IReadOnlyCollection<string> statusCodes, DateTime dueBeforeUtc);

        /// <summary>Tracked borrowings in the status that have not had an overdue notice, with equipment and reservation.</summary>
        List<Borrowing> GetTrackedInStatusNotOverdueNotified(string statusCode);

        /// <summary>Tracked borrowings in one of the statuses, without a due reminder, due within the window, with equipment.</summary>
        List<Borrowing> GetTrackedDueForReminder(IReadOnlyCollection<string> statusCodes, DateTime fromUtc, DateTime toUtc);
    }

}
