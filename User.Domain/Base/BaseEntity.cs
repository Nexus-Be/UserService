namespace User.Domain.Base;

public class BaseEntity<TKey> : IAuditable<TKey>, ISoftDelete
{
    public TKey Id { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public TKey? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public TKey? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}