using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStorage.Domain.Services;

namespace SecureStorage.Controllers;

[Authorize]
[ApiController]
[Route("api/invites")]
public class InvitesController(
    IUserService _userService
) : ControllerBase
{
    /// <summary>
    /// Generates a new invite code for the system.
    /// Accessible only by logged-in users.
    /// </summary>
    /// <param name="ct">The cancellation token.</param>
    /// <returns>The generated invite code UUID.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateInvite(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var invite = await _userService.CreateInviteAsync(userId, ct);
        return Ok(new { code = invite.Id });
    }
}
