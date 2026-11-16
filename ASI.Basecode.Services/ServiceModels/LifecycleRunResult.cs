namespace ASI.Basecode.Services.ServiceModels
{
    /// <summary>Counts from one lifecycle pass (for logging).</summary>
    public class LifecycleRunResult
    {
        public int Expired { get; set; }
        public int MarkedOverdue { get; set; }
        public int DueReminders { get; set; }
        public int ExpiryWarnings { get; set; }
    }
}
