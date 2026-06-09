using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Settings;

namespace SecureStorage.Extensions;

public static class DatabaseRegistrar
{
    public static void ConfigureDatabase(this WebApplicationBuilder builder, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Database' not found.");
        }
        var dbSettings = configuration.GetSection("Database").Get<DatabaseSettings>() ?? throw new InvalidOperationException("Database settings not found.");

        builder.Services.AddDbContext<AppDbContext>(options => options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0)), options =>
        {
            options.EnableRetryOnFailure(
                maxRetryCount: dbSettings.Retries?.MaxCount ?? 5,
                maxRetryDelay: dbSettings.Retries?.Delay ?? TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            );
        }));
    }
}
