using Microsoft.Rest;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

public class RegistryException : HttpOperationException
{
    public RegistryException()
    {
    }

    public RegistryException(string message)
        : base(message)
    {
    }
        
    public RegistryException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public IEnumerable<Error> Errors { get; set; } = Enumerable.Empty<Error>();
}
