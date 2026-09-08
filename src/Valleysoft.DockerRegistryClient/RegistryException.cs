using System.Net;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Represents an unsuccessful response from a registry.
/// </summary>
public class RegistryException : Exception
{
    /// <summary>
    /// Initializes a registry exception.
    /// </summary>
    public RegistryException()
    {
    }

    /// <summary>
    /// Initializes a registry exception with a message.
    /// </summary>
    /// <param name="message">Message describing the failure.</param>
    public RegistryException(string message)
        : base(message)
    {
    }
        
    /// <summary>
    /// Initializes a registry exception with a message and the exception that caused it.
    /// </summary>
    /// <param name="message">Message describing the failure.</param>
    /// <param name="innerException">Exception that caused the current exception.</param>
    public RegistryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// Gets or sets structured errors returned by the registry.
    /// </summary>
    public IEnumerable<Error> Errors { get; set; } = Enumerable.Empty<Error>();

    /// <summary>
    /// Gets or sets the HTTP status code returned by the registry.
    /// </summary>
    public HttpStatusCode? StatusCode { get; set; }
}
