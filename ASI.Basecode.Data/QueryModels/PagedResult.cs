using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.QueryModels
{
    /// <summary>
    /// Generic paged query result used by repository query methods.
    /// </summary>
    public class PagedResult<T>
    {
        public IReadOnlyList<T> Items { get; set; } = Array.Empty<T>();

        public int Page { get; set; }

        public int PageSize { get; set; }

        public int TotalCount { get; set; }

        public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

        public bool HasPrevious => Page > 1;

        public bool HasNext => Page < TotalPages;
    }
}
