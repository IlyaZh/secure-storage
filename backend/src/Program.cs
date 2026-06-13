using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SecureStorage.Extensions;
using SecureStorage.Middleware;


var builder = WebApplication.CreateBuilder(args);

builder.ConfigureAppConfiguration();

builder.ConfigureLogging();

builder.Services.AddControllers();

builder.ConfigureSettings(builder.Configuration);

builder.ConfigureDatabase(builder.Configuration);
builder.ConfigureAuthentication(builder.Configuration);

builder.Services.AddSingleton<HttpMetricsRegistry>();
builder.Services.AddHealthChecks()
    .AddCheck<DatabaseHealthCheck>("Database");
builder.RegisterApplicationServices();
builder.ConfigureFeatureFlags();

var app = builder.Build();

app.UseForwardedHeaders();

app.UseConfiguredLogging();
app.UseRouting();
app.UseMiddleware<RequestMetricsMiddleware>();
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/ping");
app.MapControllers();

await app.MigrateDatabaseAsync();

app.Run();
