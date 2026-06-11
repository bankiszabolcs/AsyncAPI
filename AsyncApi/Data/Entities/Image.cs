using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class Image
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string OriginalFileName { get; set; } = null!;

    public string Extension { get; set; } = null!;

    public int StatusId { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual ProcessingStatus Status { get; set; } = null!;

    public virtual User? User { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();

    public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
}
