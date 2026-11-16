using ASI.Basecode.Data.Models;
using System;
using System.Collections.Generic;

namespace ASI.Basecode.Data.Interfaces
{
    /// <summary>
    /// Data access for password reset tokens. A token is outstanding while unused and unexpired.
    /// </summary>
    public interface IPasswordResetTokenRepository
    {
        /// <summary>The user's outstanding tokens at <paramref name="nowUtc"/> (tracked).</summary>
        List<PasswordResetToken> GetTrackedOutstandingForUser(long userId, DateTime nowUtc);

        /// <summary>The outstanding token with this value at <paramref name="nowUtc"/> (tracked), or null.</summary>
        PasswordResetToken GetTrackedOutstanding(string token, DateTime nowUtc);

        /// <summary>True when an outstanding token with this value exists at <paramref name="nowUtc"/>.</summary>
        bool IsOutstanding(string token, DateTime nowUtc);

        /// <summary>Adds a token (saved by the caller's SaveChanges).</summary>
        void Add(PasswordResetToken token);
    }
}
