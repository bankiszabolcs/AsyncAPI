using System;
using System.Collections.Generic;

namespace AsyncApi.Data.Entities;

public partial class Category
{
    public Guid Id { get; set; }

    public string Title { get; set; } = null!;

    public string? Description { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public long PgmementoAuditId { get; set; }

    public virtual ICollection<Video> Videos { get; set; } = new List<Video>();
}
