using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class Subscription
{
    public Guid SubscriberId { get; set; }

    public Guid ChannelId { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual User Channel { get; set; } = null!;

    public virtual User Subscriber { get; set; } = null!;
}
