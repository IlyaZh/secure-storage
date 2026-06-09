using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Settings;

namespace SecureStorage.Extensions;

public static class SettingsRegistar
{
    public static void ConfigureSettings(this WebApplicationBuilder builder, IConfiguration configuration)
    {
        builder.Services.Configure<GoogleAuthentificationSettings>(configuration.GetSection("Authentication:Google"));
        builder.Services.Configure<AppSettings>(configuration.GetSection("AppSettings"));
        builder.Services.Configure<SecretsCleanupWorkerSettings>(configuration.GetSection("SecretsCleanupWorker"));
        builder.Services.Configure<InvitesCleanupWorkerSettings>(configuration.GetSection("InvitesCleanupWorker"));
    }
}