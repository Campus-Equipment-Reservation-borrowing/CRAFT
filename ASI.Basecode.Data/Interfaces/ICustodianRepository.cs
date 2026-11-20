using ASI.Basecode.Data.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using ASI.Basecode.Data.QueryModels;

namespace ASI.Basecode.Data.Interfaces
{
    public interface ICustodianRepository
    {
        PagedResult<Reservation> GetReservationsPaged(string search, long? statusId, int page, int pageSize, long? scopeCustodianUserId = null);

        Reservation GetReservationWithDetails(long reservationId);

        PagedResult<Borrowing> GetActiveBorrowingsPaged(string search, int page, int pageSize, long? scopeCustodianUserId = null);

        int CountActiveBorrowingsForReservation(long reservationId);

        /// <summary>Units released so far across every borrowing of the reservation.</summary>
        int SumReleasedForReservation(long reservationId);

        string GetNextTransactionNumber(int year);

        Borrowing GetBorrowingWithDetails(long borrowingId);

        /// <summary>Adds a borrowing (saved by the caller).</summary>
        void AddBorrowing(Borrowing borrowing);

        /// <summary>Reservation (untracked, with status, equipment and borrower) whose number matches case-insensitively
        /// (pass it upper-cased), limited to the custodian's routed work plus the unassigned pool (all when scope is null).</summary>
        Reservation FindReservationByNumberInScope(string upperReservationNumber, long? scopeCustodianUserId);

        /// <summary>Reservations of the equipment in one of the statuses (untracked, with borrower), in scope, by requested start.</summary>
        System.Collections.Generic.List<Reservation> GetReservationsByStatusInScope(System.Collections.Generic.IReadOnlyCollection<string> statusCodes, long? scopeCustodianUserId);
        System.Collections.Generic.List<Reservation> GetReservationsForEquipmentInScope(long equipmentId, System.Collections.Generic.IReadOnlyCollection<string> statusCodes, long? scopeCustodianUserId);

        /// <summary>Borrowings of the equipment in one of the statuses (untracked, with borrower and status), in scope, by due date.</summary>
        System.Collections.Generic.List<Borrowing> GetBorrowingsForEquipmentInScope(long equipmentId, System.Collections.Generic.IReadOnlyCollection<string> statusCodes, long? scopeCustodianUserId);
    }
}
