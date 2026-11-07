using ASI.Basecode.Data;

namespace ASI.Basecode.WebApp.Models
{
    /// <summary>
    /// View data for the shared pager partial. Keeps the page window at five buttons so long
    /// result sets render as <c>&lt; 1 2 3 4 5 … &gt;</c> instead of one link per page.
    /// </summary>
    public class PaginationModel
    {
        public int Page { get; set; }

        public int TotalPages { get; set; }

        public int TotalCount { get; set; }

        public string Info { get; set; }

        public static PaginationModel From<T>(PagedResult<T> result, string noun = "records")
        {
            if (result == null)
            {
                return new PaginationModel();
            }

            return new PaginationModel
            {
                Page = result.Page,
                TotalPages = result.TotalPages,
                TotalCount = result.TotalCount,
                Info = $"Page {result.Page} of {result.TotalPages} ({result.TotalCount} {noun})",
            };
        }
    }
}
