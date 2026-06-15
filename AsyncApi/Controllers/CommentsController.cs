using System.Security.Claims;
using AsyncApi.Data.Repositories;
using AsyncApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AsyncApi.Controllers;

[ApiController]
[Route("")]
public sealed class CommentsController(
    CommentRepository commentRepository) : ControllerBase
{
    private Guid? CurrentUserId
    {
        get
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
            return Guid.TryParse(sub, out var id) ? id : null;
        }
    }

    // GET /api/videos/{videoId}/comments — nyílt végpont
    [HttpGet("videos/{videoId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid videoId)
    {
        var comments = await commentRepository.GetByVideoIdAsync(videoId);

        var result = comments.Select(c => MapComment(c, includeReplies: true));
        return Ok(result);
    }

    // POST /api/videos/{videoId}/comments — csak bejelentkezett user
    [HttpPost("videos/{videoId:guid}/comments")]
    [Authorize]
    public async Task<IActionResult> CreateComment(Guid videoId, [FromBody] CreateCommentRequest request)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content))
            return BadRequest("Comment content is required.");

        var comment = await commentRepository.CreateAsync(videoId, userId, content, request.ParentCommentId);
        return Ok(MapComment(comment, includeReplies: false));
    }

    // PUT /api/comments/{id} — csak a saját komment szerkeszthető
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

    // DELETE /api/comments/{id} — csak a saját komment törölhető
    [HttpDelete("comments/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(Guid id)
    {
        if (CurrentUserId is not { } userId) return Unauthorized();

        var deleted = await commentRepository.DeleteAsync(id, userId);
        if (!deleted) return NotFound();

        return NoContent();
    }

    private static object MapComment(Data.Entities.Comment c, bool includeReplies) => new
    {
        id              = c.Id,
        content         = c.Content,
        createdAt       = c.CreateDate,
        updatedAt       = c.ModifyDate != c.CreateDate ? c.ModifyDate : (DateTime?)null,
        parentCommentId = c.ParentCommentId,
        user            = new { id = c.User.Id, name = c.User.DisplayName ?? c.User.Username },
        replies         = includeReplies
            ? c.InverseParentComment
                .Where(r => r.Active)
                .OrderBy(r => r.CreateDate)
                .Select(r => MapComment(r, includeReplies: false))
            : []
    };
}
