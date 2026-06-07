using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStorage.Domain.Services;

namespace SecureStorage.Controllers;

public record CreateInviteRequest(string Email);

[Authorize]
[ApiController]
[Route("api/invites")]
public class InvitesController(
    IUserService _userService
) : ControllerBase
{
    /// <summary>
    /// Generates a new invite code for the system for a specific target email.
    /// Accessible only by logged-in users.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateInvite([FromBody] CreateInviteRequest request, CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("Email is required.");
        }

        try
        {
            var invite = await _userService.CreateInviteAsync(userId, request.Email, ct);
            return Ok(new { code = invite.Id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets invites issued by the currently logged-in user with cursor pagination.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetMyInvites([FromQuery] Guid? lastInviteId, CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
        {
            return Unauthorized();
        }

        var invites = await _userService.GetUserInvitesAsync(userId, lastInviteId, ct);
        return Ok(invites);
    }
}
