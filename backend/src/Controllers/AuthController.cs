using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Enums;
using SecureStorage.Domain.Services;

namespace SecureStorage.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IUserService _userService,
    IOptionsSnapshot<AppSettings> _appSettings
) : ControllerBase
{
    /// <summary>
    /// Initiates the Google OAuth 2.0 authentication flow.
    /// Redirects the user to the Google login page. Optionally accepts an invite code to be
    /// passed to the callback for user registration purposes.
    /// </summary>
    /// <param name="inviteCode">Optional. The invite code to be associated with the user upon successful registration.</param>
    /// <returns>A challenge result that redirects the user to the Google authentication provider.</returns>
    [HttpGet("login")]
    public IActionResult Login(
        [FromQuery] string? inviteCode
    )
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback))
        };

        if (!string.IsNullOrEmpty(inviteCode))
        {
            if (!Guid.TryParse(inviteCode, out var inviteCodeGuid) || inviteCodeGuid == Guid.Empty)
            {
                return Redirect($"{_appSettings.Value.FrontendUrl}/#/login?error=invite_invalid");
            }
            properties.Items.Add("inviteCode", inviteCodeGuid.ToString());
        }

        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Handles the callback from the Google OAuth 2.0 provider. It processes the authentication result,
    /// checks if the user exists in the database, and either signs them in or registers them with an invite code.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>A redirect result to the home page upon successful authentication and registration, or an error result if authentication or registration fails.</returns>
    [HttpGet("callback")]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!authenticateResult.Succeeded)
        {
            return Redirect($"{_appSettings.Value.FrontendUrl}/#/login?error=auth_failed");
        }

        var email = authenticateResult.Principal?.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(email))
        {
            return Redirect($"{_appSettings.Value.FrontendUrl}/#/login?error=email_missing");
        }

        var user = await _userService.GetByEmailAsync(email, ct);
        if (user == null)
        {
            authenticateResult.Properties.Items.TryGetValue("inviteCode", out var inviteCodeStr);
            if (string.IsNullOrEmpty(inviteCodeStr) || !Guid.TryParse(inviteCodeStr, out var inviteCode) || inviteCode == Guid.Empty)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return Redirect($"{_appSettings.Value.FrontendUrl}/#/auth-error?reason=no_account");
            }

            var registrationResult = await _userService.RegisterWithInviteAsync(email, inviteCode, ct);
            if (registrationResult != RegistrationResult.Success)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                var errParam = registrationResult switch
                {
                    RegistrationResult.EmailMismatch => "email_mismatch",
                    RegistrationResult.AlreadyRegistered => "already_registered",
                    _ => "invite_invalid"
                };
                return Redirect($"{_appSettings.Value.FrontendUrl}/#/register?error={errParam}");
            }

            user = await _userService.GetByEmailAsync(email, ct);
        }

        var claims = new List<Claim>{
            new(ClaimTypes.Email, user!.Email),
            new(ClaimTypes.NameIdentifier, user!.Id.ToString())
        };

        // Hardcoded user groups/roles (empty list by default)
        var userGroups = new List<string> { }; 
        foreach (var group in userGroups)
        {
            claims.Add(new Claim(ClaimTypes.Role, group));
        }

        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(claimsIdentity));

        return Redirect(_appSettings.Value.FrontendUrl);
    }

    /// <summary>
    /// Logs out the current user by signing out of the authentication cookie.
    /// </summary>
    /// <returns>A redirect result that navigates the user to the home page.</returns>
    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Ok();
    }

    /// <summary>
    /// Returns the authentication status and details of the currently logged-in user.
    /// </summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser(CancellationToken ct)
    {
        if (User.Identity == null || !User.Identity.IsAuthenticated)
        {
            return Ok(new { isAuthenticated = false });
        }

        var email = User.FindFirstValue(ClaimTypes.Email);
        var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(idStr) || !Guid.TryParse(idStr, out Guid userId))
        {
            return Ok(new { isAuthenticated = false });
        }

        var usage = await _userService.GetStorageUsageAsync(userId, ct);

        return Ok(new
        {
            isAuthenticated = true,
            email,
            id = idStr,
            usedBytes = usage.UsedBytes,
            quotaBytes = usage.QuotaBytes
        });
    }
}