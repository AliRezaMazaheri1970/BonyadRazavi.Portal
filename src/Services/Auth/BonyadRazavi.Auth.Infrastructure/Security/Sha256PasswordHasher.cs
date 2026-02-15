using System.Security.Cryptography;
using System.Text;
using BonyadRazavi.Auth.Application.Abstractions;

namespace BonyadRazavi.Auth.Infrastructure.Security;

public sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string rawPassword)
    {
        var bytes = Encoding.UTF8.GetBytes(rawPassword);
        var hash = SHA256.HashData(bytes);
        return Convert.ToBase64String(hash);
    }

    public bool Verify(string hash, string rawPassword)
    {
        var rawHash = Hash(rawPassword);
        return string.Equals(hash, rawHash, StringComparison.Ordinal);
    }
}
