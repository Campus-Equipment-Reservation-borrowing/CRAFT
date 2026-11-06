using System;
using System.Security.Cryptography;

namespace ASI.Basecode.Services.Manager
{
    /// <summary>
    /// PBKDF2 (HMAC-SHA256) password hashing. No external package required.
    /// Stored format: <c>PBKDF2$iterations$saltBase64$hashBase64</c> (fits nvarchar(255)).
    /// </summary>
    public static class PasswordManager
    {
        private const int SaltSize = 16;
        private const int KeySize = 32;
        private const int DefaultIterations = 100_000;
        private const string Prefix = "PBKDF2";

        /// <summary>
        /// Hashes a plain-text password with a fresh random salt.
        /// </summary>
        public static string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentException("Password must not be empty.", nameof(password));
            }

            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, DefaultIterations, HashAlgorithmName.SHA256, KeySize);

            return string.Join('$', Prefix, DefaultIterations, Convert.ToBase64String(salt), Convert.ToBase64String(key));
        }

        /// <summary>
        /// Constant-time verification of a plain-text password against a stored hash.
        /// Returns false for malformed or placeholder hashes rather than throwing.
        /// </summary>
        public static bool Verify(string password, string storedHash)
        {
            if (string.IsNullOrEmpty(password) || string.IsNullOrWhiteSpace(storedHash))
            {
                return false;
            }

            var parts = storedHash.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix)
            {
                // Legacy/placeholder values (e.g. the old "#auth-stripped-placeholder#") never authenticate.
                return false;
            }

            if (!int.TryParse(parts[1], out var iterations) || iterations <= 0)
            {
                return false;
            }

            byte[] salt;
            byte[] expected;
            try
            {
                salt = Convert.FromBase64String(parts[2]);
                expected = Convert.FromBase64String(parts[3]);
            }
            catch (FormatException)
            {
                return false;
            }

            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }

        /// <summary>
        /// Validates the HLD password policy: min 8 chars, at least 1 uppercase and 1 digit.
        /// </summary>
        public static bool MeetsPolicy(string password)
        {
            if (string.IsNullOrEmpty(password) || password.Length < 8)
            {
                return false;
            }

            var hasUpper = false;
            var hasDigit = false;
            foreach (var ch in password)
            {
                if (char.IsUpper(ch)) hasUpper = true;
                else if (char.IsDigit(ch)) hasDigit = true;
            }

            return hasUpper && hasDigit;
        }

        /// <summary>
        /// Cryptographically random, URL-safe token used for password reset links.
        /// </summary>
        public static string NewToken()
        {
            return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
                .Replace('+', '-')
                .Replace('/', '_')
                .TrimEnd('=');
        }

        /// <summary>
        /// Random 12-character temporary password that satisfies <see cref="MeetsPolicy"/>.
        /// Look-alike characters (0/O, 1/l/I) are excluded so it can be read out loud.
        /// </summary>
        public static string GenerateTemporaryPassword()
        {
            const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string lower = "abcdefghijkmnpqrstuvwxyz";
            const string digits = "23456789";
            const string all = upper + lower + digits;

            var chars = new char[12];
            chars[0] = upper[RandomNumberGenerator.GetInt32(upper.Length)];
            chars[1] = digits[RandomNumberGenerator.GetInt32(digits.Length)];
            chars[2] = lower[RandomNumberGenerator.GetInt32(lower.Length)];
            for (var i = 3; i < chars.Length; i++)
            {
                chars[i] = all[RandomNumberGenerator.GetInt32(all.Length)];
            }

            // Fisher-Yates so the guaranteed classes aren't always in the first positions.
            for (var i = chars.Length - 1; i > 0; i--)
            {
                var j = RandomNumberGenerator.GetInt32(i + 1);
                (chars[i], chars[j]) = (chars[j], chars[i]);
            }

            return new string(chars);
        }
    }
}
