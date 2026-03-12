using System;
using System.Security.Cryptography;
using System.Text;

namespace HomeFinder.Security
{
    /// <summary>
    /// PBKDF2‑хеширование паролей.
    /// Формат хранения: {iterations}.{saltBase64}.{hashBase64}
    /// </summary>
    public static class PasswordHasher
    {
        private const int SaltSize = 16;      // 128 bit
        private const int KeySize = 32;       // 256 bit
        private const int DefaultIterations = 100_000;

        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            using var rng = RandomNumberGenerator.Create();
            var salt = new byte[SaltSize];
            rng.GetBytes(salt);

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                DefaultIterations,
                HashAlgorithmName.SHA256);

            var key = pbkdf2.GetBytes(KeySize);

            return $"{DefaultIterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored))
                return false;

            var parts = stored.Split('.', 3);
            if (parts.Length != 3) return false; // не наш формат

            if (!int.TryParse(parts[0], out var iterations) || iterations <= 0)
                return false;

            byte[] salt, storedKey;
            try
            {
                salt = Convert.FromBase64String(parts[1]);
                storedKey = Convert.FromBase64String(parts[2]);
            }
            catch
            {
                return false;
            }

            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256);

            var computedKey = pbkdf2.GetBytes(storedKey.Length);
            return CryptographicOperations.FixedTimeEquals(storedKey, computedKey);
        }
    }
}

