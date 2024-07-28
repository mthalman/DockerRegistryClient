using System.Net.Http.Headers;
using System.Text;

namespace Valleysoft.DockerRegistryClient.Credentials;

public class BasicAuthenticationCredentials : IRegistryClientCredentials
{
    public BasicAuthenticationCredentials(string? userName = null, string? password = null)
    {
        UserName = userName;
        Password = password;
    }

    public string? UserName { get; }
    public string? Password { get; }

    public Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{UserName}:{Password}")));
        return Task.CompletedTask;
    }
}
