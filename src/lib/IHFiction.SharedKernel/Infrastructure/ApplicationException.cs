namespace IHFiction.SharedKernel.Infrastructure;

public sealed class ApplicationException : Exception
{
    public ApplicationException(string message) : base(message)
    {
    }
    public ApplicationException()
    {
    }

    public ApplicationException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
