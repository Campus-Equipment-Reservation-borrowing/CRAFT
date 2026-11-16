using System;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// A database transaction opened through <see cref="IUnitOfWork.BeginTransaction"/>.
    /// Disposing without <see cref="Commit"/> rolls the transaction back.
    /// </summary>
    public interface IUnitOfWorkTransaction : IDisposable
    {
        /// <summary>
        /// Commits the transaction.
        /// </summary>
        void Commit();
    }
}
