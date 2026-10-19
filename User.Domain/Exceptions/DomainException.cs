namespace User.Domain.Exceptions;

public class DomainException : System.Exception
{
    public DomainException(string message) : base(message) { }
}