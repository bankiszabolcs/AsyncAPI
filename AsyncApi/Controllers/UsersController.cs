using System.Security.Claims;
using AsyncApi.Data.Entities;
using AsyncApi.Data.Repositories;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(
    IConfiguration configuration,
    UserRepository userRepository,
    VideoRepository videoRepository,
    StorageService storageService) : ControllerBase
{
    private readonly long _userQuotaBytes = long.Parse(configuration["Storage:UserQuotaBytes"] ?? "2147483648");
    // GET /users/me — provisioning + profil visszaadás
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var username    = User.FindFirstValue("preferred_username") ?? userId.ToString();
        var email       = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "";
        var displayName = User.FindFirstValue("name");

        var user = await userRepository.UpsertAsync(userId, username, email, displayName);

        return Ok(MapUser(user));
    }

    // PATCH /users/me — megjelenített név és avatar frissítése
    [HttpPatch("me")]
    [Authorize]
    public async Task<IActionResult> UpdateMe([FromBody] UpdateProfileRequest request)
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var displayName = request.DisplayName?.Trim();
        if (displayName == string.Empty)
            return BadRequest("Display name cannot be empty.");

        var user = await userRepository.UpdateProfileAsync(userId, displayName, request.AvatarImageId);
        if (user is null) return NotFound();

        return Ok(MapUser(user));
    }

    // GET /users/me/storage — felhasznált és szabad tárhely lekérése
    [HttpGet("me/storage")]
    [Authorize]
    public async Task<IActionResult> GetStorageInfo()
    {
        if (!TryGetUserId(out var userId)) return Unauthorized();

        var usedBytes   = await videoRepository.GetStorageUsedBytesAsync(userId);
        var freeBytes   = Math.Max(0, _userQuotaBytes - usedBytes);
        var usedPercent = _userQuotaBytes > 0
            ? Math.Round((double)usedBytes / _userQuotaBytes * 100, 1)
            : 0;

        return Ok(new { usedBytes, totalBytes = _userQuotaBytes, freeBytes, usedPercent });
    }

    private bool TryGetUserId(out Guid userId)
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out userId);
    }

    private object MapUser(User user)
    {
        string? avatarUrl = null;
        if (user.AvatarImage is { Extension: var ext })
        {
            avatarUrl = storageService.GetPublicUrl(
                $"{user.AvatarImageId}/{user.AvatarImageId}_w128{ext}",
                StorageBucket.Images);
        }

        return new
        {
            id          = user.Id,
            username    = user.Username,
            email       = user.Email,
            displayName = user.DisplayName,
            avatarUrl,
        };
    }
}
