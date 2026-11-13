namespace ECommerce.Shared.Exceptions;

/// <summary>
/// A synchronous call to another service failed outright: transport error, timeout, an
/// unexpected 5xx, or an internal configuration problem on the far side. It is mapped to
/// 502 so the caller sees "the service behind this one is unhealthy" rather than a 500 that
/// would imply this service itself misbehaved. The message is always one this service
/// composed, never content echoed from the failing service.
/// </summary>
public class DependencyUnavailableException : Exception
{
    public DependencyUnavailableException(string message) : base(message)
    {
    }

    public DependencyUnavailableException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
