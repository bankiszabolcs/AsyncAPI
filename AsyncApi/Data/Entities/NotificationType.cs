namespace AsyncApi.Data.Entities;

public partial class NotificationType
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

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
}
