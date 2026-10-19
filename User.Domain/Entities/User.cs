using User.Domain.Base;
using User.Domain.ValueObjects;

namespace User.Domain.Entities;

public class User : BaseEntity<Guid>
{
    public required string Email  { get; set; }
    public required string PasswordHash  { get; set; }
    public required string FirstName  { get; set; }
    public required string LastName  { get; set; }
    public PhoneNumber PhoneNumber { get; set; }
    
}