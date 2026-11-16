using ASI.Basecode.Data.Interfaces;
using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ASI.Basecode.Data.Repositories
{
    /// <summary>
    /// Password reset token repository implementation. Data access only.
    /// </summary>
    public class PasswordResetTokenRepository : BaseRepository, IPasswordResetTokenRepository
    {
        public PasswordResetTokenRepository(IUnitOfWork unitOfWork) : base(unitOfWork)
        {
        }

        public List<PasswordResetToken> GetTrackedOutstandingForUser(long userId, DateTime nowUtc)
        {
            return this.Context.PasswordResetTokens
                .Where(t => t.UserId == userId && t.UsedAtUtc == null && t.ExpiresAtUtc > nowUtc)
                .ToList();
        }

        public PasswordResetToken GetTrackedOutstanding(string token, DateTime nowUtc)
        {
            return this.Context.PasswordResetTokens
                .FirstOrDefault(t => t.Token == token && t.UsedAtUtc == null && t.ExpiresAtUtc > nowUtc);
        }

        public bool IsOutstanding(string token, DateTime nowUtc)
        {
            return this.Context.PasswordResetTokens
                .Any(t => t.Token == token && t.UsedAtUtc == null && t.ExpiresAtUtc > nowUtc);
        }

        public void Add(PasswordResetToken token)
        {
            this.Context.PasswordResetTokens.Add(token);
        }
    }
}
