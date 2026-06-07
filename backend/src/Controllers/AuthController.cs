using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;

namespace SecureStorage.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    IUserService _userService,
    IOptions<AppSettings> _appSettings
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
        [FromQuery] Guid? inviteCode
    )
    {
        var properties = new AuthenticationProperties
        {
            RedirectUri = Url.Action(nameof(Callback))
        };

        if (inviteCode.HasValue)
        {
            properties.Items.Add("inviteCode", inviteCode.Value.ToString());
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
            return BadRequest("Authentication failed");
        }

        var email = authenticateResult.Principal?.FindFirstValue(ClaimTypes.Email);

        if (string.IsNullOrEmpty(email))
        {
            return BadRequest("Cannot get email from Google");
        }

        var user = await _userService.GetByEmailAsync(email, ct);
        if (user == null)
        {
            authenticateResult.Properties.Items.TryGetValue("inviteCode", out var inviteCodeStr);
            if (string.IsNullOrEmpty(inviteCodeStr) || !Guid.TryParse(inviteCodeStr, out var inviteCode) || inviteCode == Guid.Empty)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied. Registration only by invites." });
            }

            var registrationSuccess = await _userService.RegisterWithInviteAsync(email, inviteCode, ct);
            if (!registrationSuccess)
            {
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Registration failed. The invite might be invalid or already used." });
            }
        }

        var claims = new List<Claim>{
            new(ClaimTypes.Email, user!.Email),
            new(ClaimTypes.NameIdentifier, user!.Id.ToString())
        };
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
}