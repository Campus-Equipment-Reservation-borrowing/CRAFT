namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// Id/text pair read from a codebook table for drop-down lists.
    /// </summary>
    public class LookupOption
    {
        public long Id { get; set; }

        public string Text { get; set; }
    }
}
