using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Borrowing repository implementation. Data access only.
    /// </summary>
    public class BorrowingRepository : BaseRepository, IBorrowingRepository
    {
        public BorrowingRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public int CountForBorrowerWithStatus(long borrowerUserId, string statusCode)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == borrowerUserId && b.Status.Code == statusCode);
        }

        public int CountForBorrowerWithoutStatuses(long borrowerUserId, string firstStatusCode, string secondStatusCode)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == borrowerUserId
                    && b.Status.Code != firstStatusCode
                    && b.Status.Code != secondStatusCode);
        }

        public int CountForBorrowerInStatusesDueBefore(long borrowerUserId, IReadOnlyCollection<string> statusCodes, DateTime dueBeforeUtc)
        {
            return this.Context.Borrowings
                .AsNoTracking()
                .Count(b => b.BorrowerUserId == borrowerUserId
                    && statusCodes.Contains(b.Status.Code)
                    && b.DueAtUtc < dueBeforeUtc);
        }
    }
}
