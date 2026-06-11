using System;
using System.Collections.Generic;
using System.Net;

namespace AsyncApi.Data.Entities;

public partial class VideoView
{
    public long Id { get; set; }

    public Guid VideoId { get; set; }

    public Guid? UserId { get; set; }

    public DateTime WatchedAt { get; set; }

    public int? WatchedSeconds { get; set; }

    public IPAddress? IpAddress { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual User? User { get; set; }

    public virtual Video Video { get; set; } = null!;
}
