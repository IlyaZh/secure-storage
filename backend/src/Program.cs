using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.EntityFrameworkCore;
using SecureStorage.Data;
using SecureStorage.Domain.Entities;
using SecureStorage.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.Configure<GoogleAuthentificationSettings>(builder.Configuration.GetSection("Authentication:Google"));
builder.Services.Configure<AppSettings>(builder.Configuration.GetSection("AppSettings"));

// Database
var connectionString = builder.Configuration.GetConnectionString("Database");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("Connection string 'Database' not found.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseMySql(connectionString, new MySqlServerVersion(new Version(8, 0))));

// Authentication
var googleConfig = builder.Configuration.GetSection("Authentication:Google").Get<GoogleAuthentificationSettings>() ?? throw new InvalidOperationException("Authentication:Google not found.");
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie()
.AddGoogle(googleOptions =>
{
    googleOptions.ClientId = googleConfig.ClientId;
    googleOptions.ClientSecret = googleConfig.ClientSecret;
    googleOptions.CallbackPath = "/signin-google";
});

builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<ISecretService, SecretService>();

var app = builder.Build();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<AppDbContext>();
        await context.Database.MigrateAsync();
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while migrating the database.");
    }
}


app.Run();
