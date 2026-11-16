using System;
using System.Collections.Generic;

namespace ASI.Basecode.Services.ViewModelClasses
{
    /// <summary>
    /// Public landing page (1.0) figures, computed from live inventory and reservation data.
    /// </summary>
    public class LandingViewModel
    {
        public int ItemsInCatalog { get; set; }

        public int CategoryCount { get; set; }

        /// <summary>Median hours from submission to a custodian decision over the last 90 days; null when there is no data.</summary>
        public double? MedianDecisionHours { get; set; }

        public DateTime UpdatedAtLocal { get; set; }

        public List<LandingCategoryViewModel> Categories { get; set; } = new List<LandingCategoryViewModel>();
    }

    public class LandingCategoryViewModel
    {
        public string Name { get; set; }

        public int Available { get; set; }

        public int Total { get; set; }

        public int PercentAvailable => this.Total <= 0 ? 0 : (int)Math.Round(100.0 * this.Available / this.Total);
    }
}
