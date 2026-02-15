namespace BonyadRazavi.Auth.Api.Security;

public interface ILoginLockoutService
{
    LockoutStatus GetStatus(string userName, string clientIp);

    LockoutStatus RegisterFailure(string userName, string clientIp);

    void RegisterSuccess(string userName, string clientIp);
}

public readonly record struct LockoutStatus(bool IsLocked, TimeSpan RetryAfter)
{
    public static LockoutStatus None => new(false, TimeSpan.Zero);
}
