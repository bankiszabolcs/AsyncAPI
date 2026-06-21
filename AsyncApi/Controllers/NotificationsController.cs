using System.Security.Claims;
using AsyncApi.Data.Repositories;
using AsyncApi.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Controllers;

[ApiController]
[Route("notifications")]
[Authorize]
public sealed class NotificationsController(NotificationRepository notificationRepository) : ControllerBase
{
    private static string MapType(int typeId) => typeId switch
    {
        (int)NotificationKind.NewVideo       => "new_video",
        (int)NotificationKind.NewComment     => "new_comment",
        (int)NotificationKind.CommentReply   => "comment_reply",
        (int)NotificationKind.VideoProcessed => "video_processed",
        _                                    => "unknown"
    };

    private Guid? CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    // GET /notifications — lista (legújabb elöl, lapozható)
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var items = await notificationRepository.GetByUserAsync(userId, page, pageSize);

        return Ok(items.Select(n => new
        {
            id         = n.Id,
            type       = MapType(n.TypeId),
            title      = n.Title,
            body       = n.Body,
            link       = n.Link,
            isRead     = n.IsRead,
            createDate = n.CreateDate,
        }));
    }

    // GET /notifications/unread-count — badge számhoz
    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var count = await notificationRepository.GetUnreadCountAsync(userId);
        return Ok(new { count });
    }

    // POST /notifications/{id}/read — egy értesítés olvasottnak jelölése
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var success = await notificationRepository.MarkReadAsync(id, userId);
        if (!success) return NotFound();

        return NoContent();
    }

    // POST /notifications/read-all — összes olvasottnak jelölése
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        await notificationRepository.MarkAllReadAsync(userId);
        return NoContent();
    }
}
