namespace BonyadRazavi.WebApp.Configuration;

public sealed class ApiGatewayOptions
{
    public const string SectionName = "ApiGateway";

    public string BaseUrl { get; set; } = "https://localhost:7100/";
}
