using System.Security.Claims;
using System.Text.Json;
using AsyncApi.Data.Repositories;
using AsyncApi.Models;
using AsyncApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using StackExchange.Redis;

namespace AsyncApi.Controllers;

[ApiController]
[Route("")]
public sealed class CommentsController(
    CommentRepository commentRepository,
    NotificationService notificationService,
    CommentSseService commentSse,
    StorageService storageService,
    IConnectionMultiplexer redis) : ControllerBase
{
    private static readonly JsonSerializerOptions _jsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    private Guid? CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    // GET /api/videos/{videoId}/comments
    [HttpGet("videos/{videoId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid videoId)
    {
        var comments = await commentRepository.GetByVideoIdAsync(videoId);
        return Ok(comments.Select(c => MapComment(c, includeReplies: true)));
    }

    // GET /api/videos/{videoId}/comments/stream — SSE
    [HttpGet("videos/{videoId:guid}/comments/stream")]
    public async Task StreamComments(Guid videoId, CancellationToken ct)
    {
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("X-Accel-Buffering", "no");
        Response.Headers.Append("Connection", "keep-alive");

        var (connId, reader) = commentSse.Subscribe(videoId);
        try
        {
            await foreach (var json in reader.ReadAllAsync(ct))
            {
                await Response.WriteAsync($"data: {json}\n\n", ct);
                await Response.Body.FlushAsync(ct);
            }
        }
        catch (OperationCanceledException) { }
        finally
        {
            commentSse.Unsubscribe(videoId, connId);
        }
    }

    // POST /api/videos/{videoId}/comments
    [HttpPost("videos/{videoId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> CreateComment(Guid videoId, [FromBody] CreateCommentRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest("Comment content is required.");

        var comment = await commentRepository.CreateAsync(videoId, userId, content, request.ParentCommentId);

        var commenterName = comment.User.DisplayName ?? comment.User.Username;
        if (request.ParentCommentId.HasValue)
            await notificationService.NotifyCommentReplyAsync(
                request.ParentCommentId.Value, comment.Id, userId, commenterName, content, videoId);
        else
            await notificationService.NotifyNewCommentAsync(videoId, comment.Id, userId, commenterName, content);

        // Publish to Redis → broadcast to SSE subscribers
        var json = JsonSerializer.Serialize(MapComment(comment, includeReplies: false), _jsonOptions);
        await redis.GetSubscriber().PublishAsync(RedisChannel.Literal($"comments:{videoId}"), json);

        return Ok(MapComment(comment, includeReplies: false));
    }

    // PUT /api/comments/{id}
    [HttpPut("comments/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateComment(Guid id, [FromBody] UpdateCommentRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest("Comment content is required.");

        var comment = await commentRepository.UpdateAsync(id, userId, content);
        if (comment is null) return NotFound();

        return Ok(MapComment(comment, includeReplies: false));
    }

    // DELETE /api/comments/{id}
    [HttpDelete("comments/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var deleted = await commentRepository.DeleteAsync(id, userId);
        if (!deleted) return NotFound();

        return NoContent();
    }

    private object MapComment(Data.Entities.Comment c, bool includeReplies) => new
    {
        id              = c.Id,
        content         = c.Content,
        createdAt       = c.CreateDate,
        updatedAt       = c.ModifyDate != c.CreateDate ? c.ModifyDate : (DateTime?)null,
        parentCommentId = c.ParentCommentId,
        user            = new
        {
            id        = c.User.Id,
            name      = c.User.DisplayName ?? c.User.Username,
            avatarUrl = c.User.AvatarImage is { Extension: var ext }
                ? storageService.GetPublicUrl($"{c.User.AvatarImageId}/{c.User.AvatarImageId}_w128{ext}", StorageBucket.Images)
                : (string?)null,
        },
        replies = includeReplies
            ? c.InverseParentComment
                .Where(r => r.Active)
                .OrderBy(r => r.CreateDate)
                .Select(r => MapComment(r, includeReplies: false))
            : []
    };
}
