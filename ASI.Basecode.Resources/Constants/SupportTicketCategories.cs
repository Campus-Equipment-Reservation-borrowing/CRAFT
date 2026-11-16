namespace ASI.Basecode.Resources.Constants
{
    /// <summary><c>support_tickets.category</c> values.</summary>
    public static class SupportTicketCategories
    {
        public const string Verification = "VERIFICATION";
        public const string Appeal = "APPEAL";
        public const string Feedback = "FEEDBACK";
        public const string Other = "OTHER";

        public static readonly string[] All = { Verification, Appeal, Feedback, Other };
    }
}
