namespace User.Domain.Base;

public interface IAuditable<TKey>
{
    public DateTimeOffset CreatedAt { get; set; }
    public TKey? CreatedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public TKey? UpdatedBy { get; set; }
}