using System;
using System.Collections.Generic;
using NpgsqlTypes;

namespace AsyncApi.Data.Entities;

public partial class Video
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public int? DurationSeconds { get; set; }

    public int StatusId { get; set; }

    public int VisibilityId { get; set; }

    public DateTime? PublishedAt { get; set; }

    public Guid? ThumbnailImageId { get; set; }

    public long ViewCount { get; set; }

    public int LikeCount { get; set; }

    public int DislikeCount { get; set; }

    public int CommentCount { get; set; }

    public NpgsqlTsVector? SearchVector { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<PlaylistVideo> PlaylistVideos { get; set; } = new List<PlaylistVideo>();

    public virtual ProcessingStatus Status { get; set; } = null!;

    public virtual Image? ThumbnailImage { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual ICollection<VideoReaction> VideoReactions { get; set; } = new List<VideoReaction>();

    public virtual ICollection<VideoTag> VideoTags { get; set; } = new List<VideoTag>();

    public virtual ICollection<VideoView> VideoViews { get; set; } = new List<VideoView>();

    public virtual Visibility Visibility { get; set; } = null!;
}
