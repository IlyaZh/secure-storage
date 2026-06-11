using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SecureStorage.Extensions;
using SecureStorage.Middleware;


var builder = WebApplication.CreateBuilder(args);

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

// Debug middleware to inspect headers from Traefik
app.Use(async (context, next) =>
{
    var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
    var forwardedProto = context.Request.Headers["X-Forwarded-Proto"].ToString();
    var forwardedFor = context.Request.Headers["X-Forwarded-For"].ToString();
    
    logger.LogInformation("[ProxyDebug] BEFORE: Scheme={Scheme}, X-Forwarded-Proto='{Proto}', X-Forwarded-For='{For}'", 
        context.Request.Scheme, forwardedProto, forwardedFor);

    await next();

    logger.LogInformation("[ProxyDebug] AFTER: Scheme={Scheme}", context.Request.Scheme);
});

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
