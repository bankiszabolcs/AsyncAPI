using AsyncApi.Data;
using AsyncApi.Data.Entities;
using AsyncApi.Enums;
using Microsoft.EntityFrameworkCore;

namespace AsyncApi.Services;

public class NotificationService(AsyncApiDbContext db)
{
    // Új publikus videó — minden feliratkozónak
    public async Task NotifyNewVideoAsync(Guid videoId)
    {
        var video = await db.Videos
            .Include(v => v.User)
            .FirstOrDefaultAsync(v => v.Id == videoId && v.Active);
        if (video is null) return;

        var channelName   = video.User.DisplayName ?? video.User.Username;
        var subscriberIds = await db.Subscriptions
            .Where(s => s.ChannelId == video.UserId && s.Active)
            .Select(s => s.SubscriberId)
            .ToListAsync();

        foreach (var subscriberId in subscriberIds)
            await CreateAsync(subscriberId, NotificationKind.NewVideo,
                channelName, Truncate(video.Title, 80), $"/watch/{video.Id}");
    }

    // Videó feldolgozás kész — a feltöltőnek
    public async Task NotifyVideoProcessedAsync(Guid videoId)
    {
        var video = await db.Videos.FindAsync(videoId);
        if (video is null) return;

        await CreateAsync(video.UserId, NotificationKind.VideoProcessed,
            "Videód elkészült!", Truncate(video.Title, 80), "/studio");
    }

    // Új komment a videódon — a videó tulajdonosának (saját kommentre nem küld)
    public async Task NotifyNewCommentAsync(Guid videoId, Guid commentId, Guid commentUserId, string commenterName, string content)
    {
        var video = await db.Videos.FindAsync(videoId);
        if (video is null || video.UserId == commentUserId) return;

        await CreateAsync(video.UserId, NotificationKind.NewComment,
            commenterName, Truncate(content, 80), $"/watch/{videoId}?highlight={commentId}");
    }

    // Válasz a kommentedre — a szülőkomment szerzőjének (saját válaszra nem küld)
    public async Task NotifyCommentReplyAsync(Guid parentCommentId, Guid replyCommentId, Guid replyUserId, string replierName, string content, Guid videoId)
    {
        var parent = await db.Comments.FindAsync(parentCommentId);
        if (parent is null || parent.UserId == replyUserId) return;

        await CreateAsync(parent.UserId, NotificationKind.CommentReply,
            replierName, Truncate(content, 80), $"/watch/{videoId}?highlight={replyCommentId}");
    }

    private async Task CreateAsync(Guid userId, NotificationKind kind, string title, string? body, string? link)
    {
        db.Notifications.Add(new Notification
        {
            Id           = Guid.NewGuid(),
            UserId       = userId,
            TypeId       = (int)kind,
            Title        = title,
            Body         = body,
            Link         = link,
            IsRead       = false,
            Active       = true,
            CreateDate   = DateTime.UtcNow,
            CreateUserId = userId,
        });
        await db.SaveChangesAsync();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";
}
