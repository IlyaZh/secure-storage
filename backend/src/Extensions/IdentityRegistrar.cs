using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using SecureStorage.Domain.Settings;

namespace SecureStorage.Extensions;

public static class IdentityRegistrar
{
    public static void ConfigureAuthentication(this WebApplicationBuilder builder, IConfiguration configuration)
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(builder.Configuration["AppSettings:FrontendUrl"] ?? "http://localhost:3005")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials();
            });
        });

        var googleConfig = configuration.GetSection("Authentication:Google").Get<GoogleAuthentificationSettings>() ?? throw new InvalidOperationException("Authentication:Google not found.");
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        })
        .AddGoogle(googleOptions =>
        {
            googleOptions.ClientId = googleConfig.ClientId;
            googleOptions.ClientSecret = googleConfig.ClientSecret;
            googleOptions.CallbackPath = "/signin-google";
        });

    }
}
