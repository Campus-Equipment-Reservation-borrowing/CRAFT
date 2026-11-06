using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    public interface ICustodianService
    {
        CustodianReservationsIndexViewModel GetReservationsIndex(string search, long? statusId, int page, int pageSize);

        ReservationReviewViewModel GetReservationReview(long reservationId);
        /// <summary>Takes an unassigned PENDING/APPROVED reservation from the shared pool.</summary>
        (bool Success, string Error) Claim(long reservationId);

        /// <summary>Custodian cancels an open reservation with a reason, releasing its held units.</summary>
        (bool Success, string Error) Cancel(long reservationId, string reason);

        /// <summary>
        /// Resolves a scanned code: a reservation number, or an equipment asset code (listing in-scope
        /// releases and returns for that item).
        /// </summary>
        ScanResultViewModel ResolveScan(string code);


        (bool Success, string Error) Approve(long reservationId, string notes);

        (bool Success, string Error) Reject(long reservationId, string reason);

        ReleaseContextViewModel GetReleaseContext(long reservationId);

        (bool Success, string Error, long BorrowingId) Release(ReleaseFormViewModel form);

        ActiveBorrowingsIndexViewModel GetActiveBorrowings(string search, int page, int pageSize);
    }
}
