namespace ASI.Basecode.Resources.Constants
{
    /// <summary>
    /// Borrowing status codes as stored in <c>borrowing_statuses.code</c>.
    /// </summary>
    public static class BorrowingStatusCodes
    {
        public const string Active = "ACTIVE";
        public const string Overdue = "OVERDUE";
        public const string PartiallyReturned = "PARTIALLY_RETURNED";
        public const string Returned = "RETURNED";
        public const string Lost = "LOST";
    }
}
