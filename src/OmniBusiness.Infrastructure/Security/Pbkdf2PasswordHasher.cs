using System.Security.Cryptography;
using System.Text;
using OmniBusiness.Application.Abstractions.Security;

namespace OmniBusiness.Infrastructure.Security;

public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    public string HashPassword(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100000,
            HashAlgorithmName.SHA256,
            32);

        return $"{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string storedHash)
    {
        var segments = storedHash.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length != 2)
        {
            return false;
        }

        var salt = Convert.FromBase64String(segments[0]);
        var expectedHash = Convert.FromBase64String(segments[1]);
        var actualHash = Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password),
            salt,
            100000,
            HashAlgorithmName.SHA256,
            expectedHash.Length);

        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
