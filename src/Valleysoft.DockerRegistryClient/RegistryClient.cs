using System.Net;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Provides access to Docker Registry HTTP API and OCI Distribution API operations.
/// </summary>
public class RegistryClient : IDisposable
{
    private readonly bool disposeHttpClient;
    private const string XmlMediaType = "application/xml";

    /// <summary>
    /// Gets the registry host and non-default port, without a URI scheme.
    /// </summary>
    public string Registry { get; }

    /// <summary>
    /// Gets the registry's absolute base URI.
    /// </summary>
    public Uri BaseUri { get; }

    /// <summary>
    /// Gets the blob operations.
    /// </summary>
    public IBlobOperations Blobs { get; }

    /// <summary>
    /// Gets the repository catalog operations.
    /// </summary>
    public ICatalogOperations Catalog { get; }

    /// <summary>
    /// Gets the repository tag operations.
    /// </summary>
    public ITagOperations Tags { get; }

    /// <summary>
    /// Gets the manifest operations.
    /// </summary>
    public IManifestOperations Manifests { get; }

    /// <summary>
    /// Gets the OCI referrer operations.
    /// </summary>
    public IReferrerOperations Referrers { get; }

    /// <summary>
    /// Gets the HTTP client used for registry requests.
    /// </summary>
    public HttpClient HttpClient { get; }

    private readonly IRegistryClientCredentials? credentials;

    /// <summary>
    /// Initializes a client that uses anonymous access and an internally managed HTTP client.
    /// </summary>
    /// <param name="registry">Registry host name or HTTP(S) origin. HTTPS is used when no scheme is specified.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="registry"/> is not an HTTP(S) origin, or contains user information, a non-root path, a query, or a fragment.</exception>
    public RegistryClient(string registry)
        : this(registry, serviceClientCredentials: null)
    {
    }

    /// <summary>
    /// Initializes a client with credentials and an internally managed HTTP client.
    /// </summary>
    /// <param name="registry">Registry host name or HTTP(S) origin. HTTPS is used when no scheme is specified.</param>
    /// <param name="serviceClientCredentials">Credentials applied to registry requests, or <see langword="null"/> for anonymous access.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="registry"/> is not an HTTP(S) origin, or contains user information, a non-root path, a query, or a fragment.</exception>
    public RegistryClient(string registry, IRegistryClientCredentials? serviceClientCredentials)
        : this(registry, serviceClientCredentials, httpClient: null)
    {
    }

    /// <summary>
    /// Initializes a registry client with optional credentials and HTTP transport.
    /// </summary>
    /// <param name="registry">Registry host name or HTTP(S) origin. HTTPS is used when no scheme is specified.</param>
    /// <param name="serviceClientCredentials">Credentials applied to registry requests, or <see langword="null"/> for anonymous access.</param>
    /// <param name="httpClient">HTTP client to use, or <see langword="null"/> to create a client with built-in bearer authentication and redirect handling.</param>
    /// <param name="disposeHttpClient">Whether disposing this instance also disposes a supplied <paramref name="httpClient"/>. An internally created client is always disposed.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="registry"/> is not an HTTP(S) origin, or contains user information, a non-root path, a query, or a fragment.</exception>
    public RegistryClient(string registry, IRegistryClientCredentials? serviceClientCredentials, HttpClient? httpClient, bool disposeHttpClient = false)
    {
        Uri registryUri = RegistryUriBuilder.CreateOrigin(registry);

        if (httpClient is null)
        {
            this.HttpClient = CreateHttpClient(
                new HttpClientHandler
                {
                    AllowAutoRedirect = false
                });
            disposeHttpClient = true;

        }
        else
        {
            HttpClient = httpClient;
        }

        this.disposeHttpClient = disposeHttpClient;

        this.Registry = registryUri.Host + (registryUri.IsDefaultPort ? string.Empty : $":{registryUri.Port}");
        this.BaseUri = registryUri;

        this.credentials = serviceClientCredentials;

        this.Blobs = new BlobOperations(this);
        this.Catalog = new CatalogOperations(this);
        this.Tags = new TagOperations(this);
        this.Manifests = new ManifestOperations(this);
        this.Referrers = new ReferrerOperations(this);
    }

    internal RegistryClient(
        string registry,
        IRegistryClientCredentials? serviceClientCredentials,
        HttpMessageHandler innerHandler)
        : this(registry, serviceClientCredentials, CreateHttpClient(innerHandler), disposeHttpClient: true)
    {
    }

    private static HttpClient CreateHttpClient(HttpMessageHandler innerHandler) =>
        new(
            new OAuthDelegatingHandler(
                new RedirectDelegatingHandler(innerHandler)));

    internal Task<T> SendRequestAsync<T>(HttpRequestMessage request, CancellationToken cancellationToken = default) =>
        SendRequestAsync(request, (Func<HttpResponseMessage, string, T>?)null, cancellationToken);

    internal async Task<T> SendRequestAsync<T>(HttpRequestMessage request,
        Func<HttpResponseMessage, string, T>? getResult, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return await GetStringContentAsync(response, getResult).ConfigureAwait(false);
    }

    internal async Task SendRequestAsync(HttpRequestMessage request, bool ignoreUnsuccessfulResponse = false, CancellationToken cancellationToken = default)
    {
        using HttpResponseMessage response = await SendRequestCoreAsync(request, ignoreUnsuccessfulResponse, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<bool> SendExistsRequestAsync(HttpRequestMessage request, CancellationToken cancellationToken = default)
    {
        try
        {
            using HttpResponseMessage response = await SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (RegistryException exception) when (exception.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }

    internal async Task<HttpResponseMessage> SendRequestCoreAsync(
        HttpRequestMessage request,
        bool ignoreUnsuccessfulResponse = false,
        CancellationToken cancellationToken = default,
        HttpCompletionOption completionOption = HttpCompletionOption.ResponseContentRead)
    {
        if (this.credentials is not null && request.Headers.Authorization is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.credentials.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await this.HttpClient.SendAsync(request, completionOption, cancellationToken).ConfigureAwait(false);

        if (ignoreUnsuccessfulResponse)
        {
            return response;
        }

        if (!response.IsSuccessStatusCode)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                string errorContent = string.Empty;
                if (response.Content is not null)
                {
#if NET5_0_OR_GREATER
                    errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                    errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
                }

                ErrorResult? errorResult = ParseErrorResult(response, errorContent);

                throw new RegistryException(
                    $"Response status code does not indicate success: {response.StatusCode}. See {nameof(RegistryException.Errors)} property for more detail. ({response.ReasonPhrase})")
                {
                    Errors = errorResult?.Errors ?? Enumerable.Empty<Error>(),
                    StatusCode = response.StatusCode
                };
            }
            catch
            {
                response.Dispose();
                throw;
            }
        }
        response.EnsureSuccessStatusCode();

        return response;
    }

    private static ErrorResult? ParseErrorResult(HttpResponseMessage response, string errorContent)
    {
        if (string.IsNullOrEmpty(errorContent))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ErrorResult?>(errorContent);
        }
        catch (JsonException)
        {
            if (response.Content.Headers.ContentType?.MediaType != XmlMediaType)
            {
                return null;
            }
        }

        try
        {
            return ParseXmlErrorResult(errorContent);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    private static ErrorResult ParseXmlErrorResult(string errorContent)
    {
        ErrorResult errorResult;
        XDocument errorContentXml = XDocument.Parse(errorContent);
        if (errorContentXml.Root is null)
        {
            throw new XmlException($"Unable to parse the error response:{Environment.NewLine}{errorContent}", null);
        }

        // Some registries like mcr.microsoft.com only return a single error element in the root of the XML
        // instead of a collection of errors so we need to handle both cases.
        if (errorContentXml.Root.Name == "Errors")
        {
            errorResult = new ErrorResult()
            {
                Errors = errorContentXml.Root.Elements("Error").Select(error => CreateErrorFromXmlElement(error)).ToArray()
            };
        }
        else
        {
            XElement errorElement = errorContentXml.Root;
            errorResult = new ErrorResult()
            {
                Errors = new Error[] { CreateErrorFromXmlElement(errorElement) }
            };
        }

        return errorResult;
    }

    private static Error CreateErrorFromXmlElement(XElement errorElement) =>
        new()
        {
            Code = errorElement.Element("Code")?.Value,
            Message = errorElement.Element("Message")?.Value
        };

    internal static async Task<T> GetStringContentAsync<T>(
        HttpResponseMessage response, Func<HttpResponseMessage, string, T>? getResult)
    {
        string content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

        getResult ??= GetResult<T>;

        try
        {
            return getResult(response, content);
        }
        catch (JsonException e)
        {
            throw new JsonException($"Unable to deserialize the response:{Environment.NewLine}{content}", e);
        }
    }

    internal static async Task<HttpOperationResponse<Stream>> GetStreamContentAsync(HttpRequestMessage request, HttpResponseMessage response)
    {
        Stream stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);

        return new HttpOperationResponse<Stream>(request, response, stream);
    }

    internal static Page<T> GetPageResult<T>(HttpResponseMessage response, string content)
    {
        string? nextLink = GetNextLinkUrl(response);
        return new Page<T>(GetResult<T>(response, content), nextLink);
    }

    internal static T GetResult<T>(HttpResponseMessage response, string content) =>
        JsonSerializer.Deserialize<T>(content) ?? throw new JsonException($"Unable to deserialize the content:{Environment.NewLine}{content}");

    private static string? GetNextLinkUrl(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Link", out IEnumerable<string>? linkValues))
        {
            string[] values = linkValues.ToArray();
            var links = new List<HttpLink>();
            foreach (string linkValue in values)
            {
                if (!HttpLink.TryParseList(linkValue, out IReadOnlyList<HttpLink>? parsedLinks))
                {
                    throw new InvalidOperationException(
                        $"Unable to parse link header '{string.Join(", ", values)}'");
                }

                links.AddRange(parsedLinks!);
            }

            HttpLink[] nextLinks = links
                // An anchor changes the link context; RFC 8288 forbids applying it as if absent.
                .Where(link => link.Anchor is null && link.HasRelationship("next"))
                .ToArray();
            if (nextLinks.Length == 0)
            {
                return null;
            }

            if (nextLinks.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Link header contains multiple next links: '{string.Join(", ", values)}'");
            }

            return nextLinks[0].Url;
        }

        return null;
    }

    /// <summary>
    /// Releases the internally owned HTTP client, or a supplied client when ownership was requested.
    /// </summary>
    public void Dispose()
    {
        if (this.disposeHttpClient)
        {
            this.HttpClient.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}
