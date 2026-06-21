using System;

namespace AsyncApi.Data.Entities;

public partial class SavedVideo
{
    public Guid UserId { get; set; }

    public Guid VideoId { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Video Video { get; set; } = null!;
}
