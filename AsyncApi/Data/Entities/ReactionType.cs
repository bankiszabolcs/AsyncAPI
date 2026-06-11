using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class ReactionType
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual ICollection<VideoReaction> VideoReactions { get; set; } = new List<VideoReaction>();
}
