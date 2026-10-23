namespace User.Domain.Exceptions;

public class DomainException : Exception
{
    public string ErrorCode { get; }

    public DomainException(string message) : base(message)
    {
        ErrorCode = "DOMAIN_ERROR";
    }

    public DomainException(string errorCode, string message) : base(message)
    {
        ErrorCode = errorCode;
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
        ErrorCode = "DOMAIN_ERROR";
    }
}

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base("ENTITY_NOT_FOUND", $"Entity '{entityName}' with key '{key}' was not found.")
    {
    }
}

public class BusinessRuleValidationException : DomainException
{
    public BusinessRuleValidationException(string message) : base("BUSINESS_RULE_VIOLATION", message)
    {
    }

    public BusinessRuleValidationException(string errorCode, string message) : base(errorCode, message)
    {
    }
}
