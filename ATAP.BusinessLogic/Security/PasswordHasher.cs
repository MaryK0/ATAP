using System.Security.Cryptography;
using System.Text;

namespace ATAP.BusinessLogic.Security;

/// <summary>Shared PBKDF2 implementation with verification of legacy SHA-256 hashes.</summary>
public static class PasswordHasher
{
    private const int Iterations = 120_000;
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(hash)}";
    }
    public static bool Verify(string password, string stored)
    {
        try
        {
            if (!stored.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal))
                return CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(password)), Convert.FromBase64String(stored));
            var parts = stored.Split('$');
            if (parts.Length != 4 || !int.TryParse(parts[1], out var iterations) || iterations < 100_000 || iterations > 1_000_000) return false;
            var salt = Convert.FromBase64String(parts[2]); var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, 32);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException) { return false; }
    }
    public static bool NeedsUpgrade(string hash) => !hash.StartsWith("pbkdf2-sha256$", StringComparison.Ordinal);
}
