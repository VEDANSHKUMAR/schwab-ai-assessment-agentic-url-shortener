namespace UrlShortener.Domain.Exceptions;

public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}

public sealed class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public sealed class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }
}

public sealed class GoneException : DomainException
{
    public GoneException(string message) : base(message)
    {
    }
}

public sealed class ValidationDomainException : DomainException
{
    public ValidationDomainException(string message) : base(message)
    {
    }
}
