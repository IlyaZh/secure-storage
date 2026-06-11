using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using SecureStorage.Domain.Settings;
using SecureStorage.Domain.Entities;

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
        var appSettings = configuration.GetSection("AppSettings").Get<AppSettings>();

        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
        })
        .AddCookie(options =>
        {
            options.Cookie.SameSite = SameSiteMode.Lax;
            if (builder.Environment.IsDevelopment())
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            }
            else
            {
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                if (!string.IsNullOrEmpty(appSettings?.CookieDomain))
                {
                    options.Cookie.Domain = appSettings.CookieDomain;
                }
            }
        })
        .AddGoogle(googleOptions =>
        {
            googleOptions.ClientId = googleConfig.ClientId;
            googleOptions.ClientSecret = googleConfig.ClientSecret;
            googleOptions.CallbackPath = "/signin-google";
            googleOptions.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
            {
                OnRedirectToAuthorizationEndpoint = context =>
                {
                    var redirectUri = context.RedirectUri;
                    if (redirectUri.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
                    {
                        redirectUri = redirectUri.Replace("http://", "https://", StringComparison.OrdinalIgnoreCase);
                    }
                    context.Response.Redirect(redirectUri);
                    return Task.CompletedTask;
                }
            };
        });

    }
}
