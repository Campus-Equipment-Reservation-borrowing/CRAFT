using Microsoft.EntityFrameworkCore;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Unit of Work Interface
    /// </summary>
    public interface IUnitOfWork
    {
        /// <summary>
        /// Gets the database context
        /// </summary>
        /// <value>
        /// The database.
        /// </value>
        DbContext Database { get; }
        /// <summary>
        /// Saves the changes to database
        /// </summary>
        void SaveChanges();

        /// <summary>
        /// Begins a database transaction; dispose without committing to roll back.
        /// </summary>
        IUnitOfWorkTransaction BeginTransaction();
    }
}
