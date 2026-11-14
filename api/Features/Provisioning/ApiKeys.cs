using System.Security.Cryptography;
using System.Text;

namespace Console.Api.Features.Provisioning;

// Clinic API keys look like ck_<8 hex>_<48 hex>. Only the SHA-256 hash and the prefix are stored.
public static class ApiKeys
{
    public static (string Key, string Prefix, string Hash) Generate()
    {
        var prefix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var key = $"ck_{prefix}_{secret}";
        return (key, prefix, Hash(key));
    }

    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public static bool Matches(string? presented, string? storedHash)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(storedHash))
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(Hash(presented)), Encoding.ASCII.GetBytes(storedHash));
    }
}
