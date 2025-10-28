namespace PMSIntegration.Application.Exceptions;

public class MaxRetriesExceededException : Exception
{
    public MaxRetriesExceededException(string message) : base(message) { }
    public MaxRetriesExceededException(string message, Exception innerException) 
        : base(message, innerException) { }
}