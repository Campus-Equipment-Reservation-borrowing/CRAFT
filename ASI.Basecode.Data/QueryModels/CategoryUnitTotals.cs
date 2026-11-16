namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// Unit totals of the active equipment in one category (Available may be negative; callers clamp).
    /// </summary>
    public class CategoryUnitTotals
    {
        public string Name { get; set; }

        public int Total { get; set; }

        public int Available { get; set; }
    }
}
