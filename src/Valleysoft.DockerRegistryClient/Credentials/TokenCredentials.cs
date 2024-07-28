using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient.Credentials;

public class TokenCredentials : IRegistryClientCredentials
{
    public TokenCredentials(string token, string tokenType = "Bearer")
    {
        Token = token;
        TokenType = tokenType;
    }

    public string Token { get; }
    public string TokenType { get; }

    public Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(this.TokenType, this.Token);
        return Task.CompletedTask;
    }
}
