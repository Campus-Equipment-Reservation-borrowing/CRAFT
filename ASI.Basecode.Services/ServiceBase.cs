using Microsoft.Extensions.Logging;

namespace ASI.Basecode.Services
{
    /// <summary>
    /// Service Base class implementation
    /// </summary>
    public class ServiceBase
    {
        protected ILogger _logger;

        /// <summary>
        /// Initializes a new instance of the ServiceBase class.
        /// </summary>
        /// <param name="loggerFactory">Logger factory.</param>
        public ServiceBase(ILoggerFactory loggerFactory)
        {
            this._logger = loggerFactory.CreateLogger<ServiceBase>();
        }

        /// <summary>
        /// True when a save failed because it hit the named unique index (e.g. two users generated the same
        /// sequence number at once), so the caller can pick a new number and retry.
        /// </summary>
        protected static bool IsUniqueIndexViolation(Microsoft.EntityFrameworkCore.DbUpdateException ex, string indexName)
        {
            for (System.Exception e = ex; e != null; e = e.InnerException)
            {
                if (e.Message != null && e.Message.Contains(indexName, System.StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
