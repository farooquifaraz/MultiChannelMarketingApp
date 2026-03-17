namespace MarketingApp.Domain.Exceptions;

public class AppValidationException : Exception
{
    public IEnumerable<string> Errors { get; }

    public AppValidationException(string message) : base(message)
    {
        Errors = new[] { message };
    }

    public AppValidationException(IEnumerable<string> errors) : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }
}
