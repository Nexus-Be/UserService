using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class SystemConfig : BaseEntity<Guid>
{
    public required string ConfigKey { get; set; }
    public required string ConfigValue { get; set; }
    public required ConfigDataType DataType { get; set; } = ConfigDataType.String;
    public string? Description { get; set; }
    /// <summary>Lowest allowed value for numeric settings (null means no lower limit).</summary>
    public decimal? MinValue { get; set; }
    /// <summary>Highest allowed value for numeric settings (null means no upper limit).</summary>
    public decimal? MaxValue { get; set; }
    public Guid? UpdatedBy { get; set; }
}
