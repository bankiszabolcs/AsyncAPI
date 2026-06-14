using System.Security.Claims;
using AsyncApi.Data.Repositories;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Controllers;

[ApiController]
[Route("users")]
public sealed class UsersController(UserRepository userRepository) : ControllerBase
{
    // GET /users/me — provisioning + profil visszaadás
    // Első hívásnál létrehozza a usert a JWT claimek alapján, utána upserteli.
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (sub is null || !Guid.TryParse(sub, out var userId))
            return Unauthorized();

        var username    = User.FindFirstValue("preferred_username") ?? sub;
        var email       = User.FindFirstValue(ClaimTypes.Email) ?? User.FindFirstValue("email") ?? "";
        var displayName = User.FindFirstValue("name");

        var user = await userRepository.UpsertAsync(userId, username, email, displayName);

        return Ok(new
        {
            id          = user.Id,
            username    = user.Username,
            email       = user.Email,
            displayName = user.DisplayName,
        });
    }
}
