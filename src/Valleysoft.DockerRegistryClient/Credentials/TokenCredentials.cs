using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient.Credentials;

/// <summary>
/// Applies a pre-issued authentication token to registry requests.
/// </summary>
public class TokenCredentials : IRegistryClientCredentials
{
    /// <summary>
    /// Initializes token credentials.
    /// </summary>
    /// <param name="token">Token sent in the HTTP Authorization header.</param>
    /// <param name="tokenType">Authorization scheme, such as <c>Bearer</c>.</param>
    public TokenCredentials(string token, string tokenType = "Bearer")
    {
        Token = token;
        TokenType = tokenType;
    }

    /// <summary>
    /// Gets the authentication token.
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the HTTP authorization scheme.
    /// </summary>
    public string TokenType { get; }

    /// <inheritdoc />
    public Task ProcessHttpRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue(this.TokenType, this.Token);
        return Task.CompletedTask;
    }
}
