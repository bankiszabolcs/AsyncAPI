namespace AsyncApi.Data.Entities;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public int TypeId { get; set; }

    public string Title { get; set; } = null!;

    public string? Body { get; set; }

    public string? Link { get; set; }

    public bool IsRead { get; set; }

    public Guid? CreateUserId { get; set; }

    public DateTime CreateDate { get; set; }

    public Guid? ModifyUserId { get; set; }

    public DateTime? ModifyDate { get; set; }

    public bool Active { get; set; }

    public int Version { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual NotificationType NotificationType { get; set; } = null!;
}
