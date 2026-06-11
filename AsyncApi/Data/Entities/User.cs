using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string Username { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string? DisplayName { get; set; }

    public Guid? AvatarImageId { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual Image? AvatarImage { get; set; }

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Image> Images { get; set; } = new List<Image>();

    public virtual ICollection<Playlist> Playlists { get; set; } = new List<Playlist>();

    public virtual ICollection<Subscription> SubscriptionChannels { get; set; } = new List<Subscription>();

    public virtual ICollection<Subscription> SubscriptionSubscribers { get; set; } = new List<Subscription>();

    public virtual ICollection<VideoReaction> VideoReactions { get; set; } = new List<VideoReaction>();

    public virtual ICollection<VideoView> VideoViews { get; set; } = new List<VideoView>();

    public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
}
