using User.Domain.Base;
using User.Domain.Enums;

namespace User.Domain.Entities;

public class SystemConfig : BaseEntity<Guid>
{
    public required string ConfigKey { get; set; }
    public required string ConfigValue { get; set; }
    public required ConfigDataType DataType { get; set; } = ConfigDataType.String;
    public string? Description { get; set; }
    public Guid? UpdatedBy { get; set; }
}
