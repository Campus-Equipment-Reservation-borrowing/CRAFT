using ASI.Basecode.Data.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace ASI.Basecode.Data
{
    /// <summary>
    /// Wraps an EF Core <see cref="IDbContextTransaction"/> so services stay free of EF types.
    /// </summary>
    internal sealed class UnitOfWorkTransaction : IUnitOfWorkTransaction
    {
        private readonly IDbContextTransaction _transaction;

        public UnitOfWorkTransaction(IDbContextTransaction transaction)
        {
            _transaction = transaction;
        }

        public void Commit()
        {
            _transaction.Commit();
        }

        public void Dispose()
        {
            _transaction.Dispose();
        }
    }
}
