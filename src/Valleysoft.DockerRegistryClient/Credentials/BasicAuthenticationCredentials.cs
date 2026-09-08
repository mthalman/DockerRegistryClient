using System.Net.Http.Headers;
using System.Text;

namespace Valleysoft.DockerRegistryClient.Credentials;

/// <summary>
/// Applies HTTP Basic authentication credentials to registry requests.
/// </summary>
public class BasicAuthenticationCredentials : IRegistryClientCredentials
{
    /// <summary>
    /// Initializes Basic authentication credentials.
    /// </summary>
    /// <param name="userName">User name sent to the registry.</param>
    /// <param name="password">Password sent to the registry.</param>
    public BasicAuthenticationCredentials(string? userName = null, string? password = null)
    {
        UserName = userName;
        Password = password;
    }

    /// <summary>
    /// Gets the user name sent to the registry.
    /// </summary>
    public string? UserName { get; }

    /// <summary>
    /// Gets the password sent to the registry.
    /// </summary>
    public string? Password { get; }

    /// <inheritdoc />
    public Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserName}:{Password}")));
        return Task.CompletedTask;
    }
}
