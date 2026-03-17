namespace MarketingApp.Domain.Exceptions;

public class ExternalServiceException : Exception
{
    public string ServiceName { get; }
    public ExternalServiceException(string serviceName, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ServiceName = serviceName;
    }
}
