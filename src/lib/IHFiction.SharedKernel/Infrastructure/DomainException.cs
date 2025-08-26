namespace IHFiction.SharedKernel.Infrastructure;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }
    public DomainException()
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}