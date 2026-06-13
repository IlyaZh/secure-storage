using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using SecureStorage.BackgroundServices;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;
using SecureStorage.Extensions;

namespace SecureStorage.Extensions;

public static class ServiceRegistrar
{
    public static void RegisterApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.KnownNetworks.Clear();
            options.KnownProxies.Clear();
        });

        builder.Services.AddScoped<IUserService, UserService>();
        builder.Services.AddScoped<ISecretService, SecretService>();
        builder.Services.AddScoped<IInvitesService, InvitesService>();

        builder.Services.AddSingleton<StorageMetricsRegistry>();

        builder.Services.AddHostedService<SecretsCleanupWorker>();
        builder.Services.AddHostedService<InvitesCleanupWorker>();
        builder.Services.AddHostedService<StorageMetricsUpdateWorker>();
    }
}