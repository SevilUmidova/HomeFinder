using System;
using System.Security.Cryptography;
using System.Text;

namespace HomeFinder.Security
{
    /// <summary>
    /// Хеширование паролей через SHA2‑512.
    /// Формат хранения: HEX‑строка длиной 128 символов (без "0x").
    /// Совместимо с T‑SQL: CONVERT(varchar(128), HASHBYTES('SHA2_512', password), 2)
    /// </summary>
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            if (password == null) throw new ArgumentNullException(nameof(password));

            using var sha = SHA512.Create();

            // SQL Server HASHBYTES('SHA2_512', <varchar>) работает с однобайтной кодировкой.
            // Для паролей в ASCII‑диапазоне этого достаточно и даёт совпадение с T‑SQL.
            var bytes = Encoding.ASCII.GetBytes(password);
            var hash = sha.ComputeHash(bytes);

            var sb = new StringBuilder(hash.Length * 2);
            foreach (var b in hash)
            {
                sb.Append(b.ToString("X2")); // верхний регистр, как CONVERT(..., 2)
            }

            return sb.ToString();
        }

        public static bool Verify(string password, string stored)
        {
            if (string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(stored))
                return false;

            var computed = Hash(password);
            return string.Equals(computed, stored, StringComparison.OrdinalIgnoreCase);
        }
    }
}

