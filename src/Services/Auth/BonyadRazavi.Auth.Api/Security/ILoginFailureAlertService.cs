namespace BonyadRazavi.Auth.Api.Security;

public interface ILoginFailureAlertService
{
    void RegisterFailure(string userName, string clientIp);
    void RegisterSuccess(string userName, string clientIp);
}
