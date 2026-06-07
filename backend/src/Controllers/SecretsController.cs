using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureStorage.Domain.Enums;
using SecureStorage.Domain.Services;

namespace SecureStorage.Controllers;

[Authorize]
[ApiController]
[Route("api/secrets")]
public class SecretsController(ISecretService _secretService) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateSecret(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
        {
            return Unauthorized();
        }

        var comment = Request.Headers["X-Secret-Comment"].ToString();
        if (string.IsNullOrEmpty(comment))
        {
            return BadRequest("(Comment) Comment hasn't been found.");
        }

        if (!bool.TryParse(Request.Headers["X-Secret-IsOneTime"], out var isOneTime))
        {
            isOneTime = false;
        }

        var contentType = Request.Headers["X-Secret-ContentType"].ToString();
        if (string.IsNullOrEmpty(contentType)) contentType = "application/octet-stream";

        var fileName = Request.Headers["X-Secret-FileName"].ToString();
        var ivBase64 = Request.Headers["X-Secret-IV"].ToString();
        if (string.IsNullOrEmpty(ivBase64))
        {
            return BadRequest("(IV) Initialization vector hasn't been found.");
        }

        byte[] iv = Convert.FromBase64String(ivBase64);

        var contentStream = Request.Body;

        try
        {
            if (!Enum.TryParse(contentType, true, out ContentType parsedContentType))
            {
                return BadRequest("Invalid content type.");
            }

            var secretId = await _secretService.CreateSecretAsync(
                contentStream, userId, comment, isOneTime, iv, parsedContentType, fileName, ct);

            return Ok(new { id = secretId });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized();
        }

    }

    [HttpGet("{secretId}")]
    public async Task<IActionResult> GetSecret(Guid secretId, CancellationToken ct)
    {
        var secret = await _secretService.GetSecretAsync(secretId, ct);
        if (secret == null)
        {
            return NotFound();
        }
        return Ok(secret);
    }

    [HttpGet("my")]
    [Authorize]
    public async Task<IActionResult> GetMySecrets([FromQuery] Guid? lastSecretId,
                                                  CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
        {
            return Unauthorized();
        }

        var secrets = await _secretService.GetUserSecretsAsync(userId, lastSecretId, ct);

        return Ok(secrets);
    }
}
