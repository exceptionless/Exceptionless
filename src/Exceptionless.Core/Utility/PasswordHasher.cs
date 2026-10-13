using System.Globalization;
using System.Security.Cryptography;
using Exceptionless.Core.Extensions;

namespace Exceptionless.Core.Utility;

public static class PasswordHasher
{
    private const string Algorithm = "pbkdf2-sha256";
    private const int Iterations = 600_000;

    public static string CreateSalt() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(16));

    public static string Hash(string password, string salt)
    {
        byte[] bytes = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(salt), Iterations, HashAlgorithmName.SHA256, 32);
        return $"{Algorithm}${Iterations}${Convert.ToBase64String(bytes)}";
    }

    public static bool NeedsUpgrade(string hash) => !hash.StartsWith($"{Algorithm}${Iterations}$", StringComparison.Ordinal);

    public static bool Verify(string password, string? salt, string? hash)
    {
        if (String.IsNullOrEmpty(salt) || String.IsNullOrEmpty(hash))
            return false;
        try
        {
            byte[] saltBytes = Convert.FromBase64String(salt);
            if (saltBytes.Length == 0)
                return false;
            if (hash.Contains('$'))
            {
                string[] parts = hash.Split('$');
                if (parts.Length != 3 || parts[0] != Algorithm || saltBytes.Length != 16
                    || !Int32.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out int iterations)
                    || iterations is <= 0 or > 2_000_000)
                    return false;
                byte[] expected = Convert.FromBase64String(parts[2]);
                if (expected.Length != 32)
                    return false;
                byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, iterations, HashAlgorithmName.SHA256, expected.Length);
                return CryptographicOperations.FixedTimeEquals(actual, expected);
            }
            // Keep existing records readable until their next successful password login.
            byte[] legacyExpected = Convert.FromBase64String(hash);
            if (legacyExpected.Length != 32)
                return false;
            byte[] legacyActual = Convert.FromBase64String(password.ToSaltedHash(salt));
            return CryptographicOperations.FixedTimeEquals(legacyActual, legacyExpected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
