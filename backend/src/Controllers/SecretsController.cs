using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SecureStorage.Domain.Entities;
using SecureStorage.Domain.Services;

namespace SecureStorage.Controllers;

[Authorize]
[ApiController]
[Route("api/secrets")]
public class SecretsController(
    ISecretService _secretService,
    IOptions<AppSettings> _appSettings
) : ControllerBase
{
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> CreateSecret(CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        Console.WriteLine($"[SecretsController] CreateSecret: userIdStr = '{userIdStr}'");
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
        {
            Console.WriteLine("[SecretsController] CreateSecret: userIdStr is empty or not a valid Guid");
            return Unauthorized();
        }

        if (Request.ContentLength.HasValue && Request.ContentLength.Value > _appSettings.Value.MaxSecretSizeBytes)
        {
            return BadRequest("Secret size exceeds maximum limit.");
        }

        var maxRequestBodySizeFeature = HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>();
        if (maxRequestBodySizeFeature != null)
        {
            maxRequestBodySizeFeature.MaxRequestBodySize = _appSettings.Value.MaxSecretSizeBytes;
        }

        var commentHeader = Request.Headers["X-Secret-Comment"].ToString();
        if (string.IsNullOrEmpty(commentHeader))
        {
            return BadRequest("(Comment) Comment hasn't been found.");
        }
        var comment = Uri.UnescapeDataString(commentHeader);

        if (!bool.TryParse(Request.Headers["X-Secret-IsOneTime"], out var isOneTime))
        {
            isOneTime = false;
        }

        var contentType = Request.Headers["X-Secret-ContentType"].ToString();
        if (string.IsNullOrEmpty(contentType))
        {
            contentType = "application/octet-stream";
        }

        var fileNameHeader = Request.Headers["X-Secret-FileName"].ToString();
        var fileName = string.IsNullOrEmpty(fileNameHeader) ? "" : Uri.UnescapeDataString(fileNameHeader);
        var ivBase64 = Request.Headers["X-Secret-IV"].ToString();
        if (string.IsNullOrEmpty(ivBase64))
        {
            return BadRequest("(IV) Initialization vector hasn't been found.");
        }

        byte[] iv = Convert.FromBase64String(ivBase64);

        var contentStream = Request.Body;

        var secretId = await _secretService.CreateSecretAsync(
            contentStream, userId, comment, isOneTime, iv, contentType, fileName, ct);

        return Ok(new { id = secretId });

    }

    [HttpGet("{secretId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetSecret(Guid secretId, CancellationToken ct)
    {
        Guid? currentUserId = null;
        var userIdStr = User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!string.IsNullOrEmpty(userIdStr) && Guid.TryParse(userIdStr, out var parsedUserId))
        {
            currentUserId = parsedUserId;
        }

        var secret = await _secretService.GetSecretAsync(secretId, currentUserId, ct);
        if (secret == null)
        {
            return NotFound();
        }
        return Ok(secret);
    }

    [HttpDelete("{secretId}")]
    [Authorize]
    public async Task<IActionResult> BurnSecret(Guid secretId, CancellationToken ct)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out Guid userId))
        {
            return Unauthorized();
        }

        await _secretService.BurnSecretAsync(secretId, userId, ct);
        return NoContent();
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
