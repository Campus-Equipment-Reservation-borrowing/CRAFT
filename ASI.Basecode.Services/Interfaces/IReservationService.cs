using ASI.Basecode.Services.ViewModelClasses;
using System.Collections.Generic;

namespace ASI.Basecode.Services.Interfaces
{
    /// <summary>
    /// Borrower reservation flow service (Figma B screens): availability listing,
    /// reservation create with policy checks, own-reservation list/details and cancel.
    /// </summary>
    public interface IReservationService
    {
        /// <summary>
        /// Borrower equipment availability list with search, category filter and paging.
        /// </summary>
        EquipmentAvailabilityIndexViewModel GetAvailabilityViewModel(string search, long? categoryId, int page, int pageSize);

        /// <summary>
        /// Everything the reservation form screen needs (equipment summary + pickup points).
        /// Null when the equipment does not exist or is inactive.
        /// </summary>
        ReservationFormContextViewModel GetReservationFormViewModel(long equipmentId);

        /// <summary>
        /// Borrower equipment details (screen 2.2). Null when the equipment does not exist or is inactive.
        /// </summary>
        EquipmentDetailsViewModel GetEquipmentDetailsViewModel(long equipmentId);

        /// <summary>
        /// Validates the request against the effective borrowing policy (or built-in defaults),
        /// creates the PENDING reservation, holds units on the inventory balance and writes an audit row.
        /// </summary>
        (bool Success, string Error, long ReservationId) Create(ReservationFormViewModel form);

        /// <summary>
        /// Why the signed-in borrower can't reserve right now (account not active, or no custodian
        /// responsible for them / for <paramref name="equipmentId"/>); null when they can.
        /// </summary>
        string GetReserveBlockReason(long? equipmentId = null);

        /// <summary>
        /// The acting borrower's reservations, newest first.
        /// Empty (instead of null) when no borrower is selected in this session.
        /// </summary>
        ReservationMineViewModel GetMineViewModel();

        /// <summary>
        /// Read model for the reservation details screen. Returns null when the record is missing
        /// or the acting user may not view it (not the owner and not staff).
        /// </summary>
        ReservationDetailsViewModel GetDetailsViewModel(long reservationId);

        /// <summary>
        /// Cancels the borrower's own reservation while it is still PENDING or APPROVED.
        /// APPROVED reservations release the held units back onto the inventory balance.
        /// </summary>
        (bool Success, string Error) Cancel(long reservationId);

        /// <summary>
        /// Effective borrowing policy (latest version row, or built-in defaults when none exists).
        /// Shared with the future System Settings module so rules stay DB-driven.
        /// </summary>
        PolicySettings GetActivePolicy();

        /// <summary>
        /// Active pickup locations for the reservation form dropdown (IsPickupLocation = true).
        /// </summary>
        List<LookupViewModel> GetPickupLocationOptions();

        /// <summary>
        /// Human readable name of the acting borrower (session operator), or null.
        /// </summary>
        string GetActiveOperatorName();
    }
}
