using AsyncApi.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Data.Repositories;

public class CommentRepository(AsyncApiDbContext db)
{
    // Top-level kommentek + 1 szint mélyebb válaszok, legújabb elöl
    public async Task<List<Comment>> GetByVideoIdAsync(Guid videoId)
    {
        return await db.Comments
            .Include(c => c.User)
            .Include(c => c.InverseParentComment.Where(r => r.Active))
                .ThenInclude(r => r.User)
            .Where(c => c.VideoId == videoId && c.Active && c.ParentCommentId == null)
            .OrderByDescending(c => c.CreateDate)
            .ToListAsync();
    }

    public async Task<Comment> CreateAsync(
        Guid videoId, Guid userId, string content, Guid? parentCommentId)
    {
        var comment = new Comment
        {
            Id              = Guid.NewGuid(),
            VideoId         = videoId,
            UserId          = userId,
            ParentCommentId = parentCommentId,
            Content         = content,
            CreateUserId    = userId,
            CreateDate      = DateTime.UtcNow,
            ModifyUserId    = userId,
            Active          = true,
        };

        db.Comments.Add(comment);
        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE videos SET comment_count = comment_count + 1 WHERE id = {0}", videoId);

        return await db.Comments
            .Include(c => c.User)
            .FirstAsync(c => c.Id == comment.Id);
    }

    // null visszatérés: nem létezik vagy nem a user sajátja
    public async Task<Comment?> UpdateAsync(Guid id, Guid userId, string content)
    {
        var comment = await db.Comments
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.Active);

        if (comment is null) return null;

        comment.Content      = content;
        comment.ModifyUserId = userId;
        comment.ModifyDate   = DateTime.UtcNow;

        await db.SaveChangesAsync();
        return comment;
    }

    // false visszatérés: nem létezik vagy nem a user sajátja
    public async Task<bool> DeleteAsync(Guid id, Guid userId)
    {
        var comment = await db.Comments
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId && c.Active);

        if (comment is null) return false;

        comment.Active       = false;
        comment.ModifyUserId = userId;
        comment.ModifyDate   = DateTime.UtcNow;

        await db.SaveChangesAsync();

        await db.Database.ExecuteSqlRawAsync(
            "UPDATE videos SET comment_count = GREATEST(comment_count - 1, 0) WHERE id = {0}",
            comment.VideoId);

        return true;
    }
}
