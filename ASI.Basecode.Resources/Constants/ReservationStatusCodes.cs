namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Reservation status codes as stored in <c>reservation_statuses.code</c>.
    /// </summary>
    public static class ReservationStatusCodes
    {
        public const string Pending = "PENDING";
        public const string Approved = "APPROVED";
        public const string Rejected = "REJECTED";
        public const string Cancelled = "CANCELLED";
        public const string ReadyForRelease = "READY_FOR_RELEASE";
        public const string Completed = "COMPLETED";
        public const string Expired = "EXPIRED";
        public const string PartiallyReleased = "PARTIALLY_RELEASED";
    }
}
