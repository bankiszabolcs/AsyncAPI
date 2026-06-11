using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class PlaylistVideo
{
    public Guid PlaylistId { get; set; }

    public Guid VideoId { get; set; }

    public int Position { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual Playlist Playlist { get; set; } = null!;

    public virtual Video Video { get; set; } = null!;
}
