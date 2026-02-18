using BonyadRazavi.WebApp.Components;
using BonyadRazavi.WebApp.Configuration;
using BonyadRazavi.WebApp.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.Configure<ApiGatewayOptions>(builder.Configuration.GetSection(ApiGatewayOptions.SectionName));
builder.Services.AddScoped<UserSession>();
builder.Services.AddHttpClient<AuthApiClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;
    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        throw new InvalidOperationException("ApiGateway:BaseUrl is not configured with a valid absolute URL.");
    }

    httpClient.BaseAddress = baseAddress;
});
builder.Services.AddHttpClient<UsersApiClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;
    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        throw new InvalidOperationException("ApiGateway:BaseUrl is not configured with a valid absolute URL.");
    }

    httpClient.BaseAddress = baseAddress;
});
builder.Services.AddHttpClient<ChangePasswordApiClient>((serviceProvider, httpClient) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<ApiGatewayOptions>>().Value;
    if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseAddress))
    {
        throw new InvalidOperationException("ApiGateway:BaseUrl is not configured with a valid absolute URL.");
    }

    httpClient.BaseAddress = baseAddress;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
