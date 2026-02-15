namespace BonyadRazavi.Auth.Domain.Entities;

public sealed class UserAccount
{
    public UserAccount(
        Guid id,
        string userName,
        string displayName,
        string passwordHash,
        IReadOnlyCollection<string> roles)
    {
        Id = id;
        UserName = userName;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        Roles = roles;
    }

    public Guid Id { get; }
    public string UserName { get; }
    public string DisplayName { get; }
    public string PasswordHash { get; }
    public IReadOnlyCollection<string> Roles { get; }
}
