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
    }
}
