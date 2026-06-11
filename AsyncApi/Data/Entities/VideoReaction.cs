using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class VideoReaction
{
    public Guid VideoId { get; set; }

    public Guid UserId { get; set; }

    public int ReactionTypeId { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual ReactionType ReactionType { get; set; } = null!;

    public virtual User User { get; set; } = null!;

    public virtual Video Video { get; set; } = null!;
}
