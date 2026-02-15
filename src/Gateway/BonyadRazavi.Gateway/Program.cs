var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseHttpsRedirection();

app.MapGet("/gateway/health", () => Results.Ok(new
{
    service = "BonyadRazavi.Gateway",
    status = "Healthy",
    utc = DateTime.UtcNow
}));

app.MapReverseProxy();

app.Run();
