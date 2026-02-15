namespace BonyadRazavi.Auth.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string rawPassword);
    bool Verify(string hash, string rawPassword);
}
