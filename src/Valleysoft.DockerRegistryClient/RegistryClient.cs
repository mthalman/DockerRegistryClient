using System.Text.Json;
using System.Xml;
using System.Xml.Linq;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

public class RegistryClient : IDisposable
{
    private readonly bool disposeHttpClient;
    private const string XmlMediaType = "application/xml";

    public string Registry { get; }
    public Uri BaseUri { get; }
    public IBlobOperations Blobs { get; }
    public ICatalogOperations Catalog { get; }
    public ITagOperations Tags { get; }
    public IManifestOperations Manifests { get; }
    public HttpClient HttpClient { get; }

    private readonly IRegistryClientCredentials? credentials;

    public RegistryClient(string registry)
        : this(registry, serviceClientCredentials: null)
    {
    }

    public RegistryClient(string registry, IRegistryClientCredentials? serviceClientCredentials)
        : this(registry, serviceClientCredentials, httpClient: null, disposeHttpClient: false)
    {
            
    }

    private RegistryClient(string registry, IRegistryClientCredentials? serviceClientCredentials, HttpClient? httpClient, bool disposeHttpClient)
    {
        if (httpClient is null)
        {
            this.HttpClient = new HttpClient(new OAuthDelegatingHandler(new HttpClientHandler()));
            disposeHttpClient = true;

        }
        else
        {
            HttpClient = httpClient;
        }

        this.disposeHttpClient = disposeHttpClient;

        this.Registry = registry ?? throw new ArgumentNullException(nameof(registry));
        this.BaseUri = new Uri($"https://{this.Registry}");

        this.credentials = serviceClientCredentials;

        this.Blobs = new BlobOperations(this);
        this.Catalog = new CatalogOperations(this);
        this.Tags = new TagOperations(this);
        this.Manifests = new ManifestOperations(this);
    }

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
        using HttpResponseMessage response = await SendRequestCoreAsync(request, ignoreUnsuccessfulResponse: true, cancellationToken).ConfigureAwait(false);
        return response.IsSuccessStatusCode;
    }

    internal async Task<HttpResponseMessage> SendRequestCoreAsync(HttpRequestMessage request, bool ignoreUnsuccessfulResponse = false, CancellationToken cancellationToken = default)
    {
        if (this.credentials is not null && request.Headers.Authorization is null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await this.credentials.ProcessHttpRequestAsync(request, cancellationToken).ConfigureAwait(false);
        }

        cancellationToken.ThrowIfCancellationRequested();
        HttpResponseMessage response = await this.HttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);

        if (ignoreUnsuccessfulResponse)
        {
            return response;
        }

        if (!response.IsSuccessStatusCode)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (response.Content is null)
            {
                throw new InvalidOperationException($"Response content is null.");
            }

            string? requestContent = null;
            if (request.Content is not null)
            {
#if NET5_0_OR_GREATER
                requestContent = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
                requestContent = await request.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif
            }

#if NET5_0_OR_GREATER
            string errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
#else
            string errorContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
#endif

            ErrorResult? errorResult;

            // Handle special case for some registries like mcr.microsoft.com that can return an XML error response
            // instead of JSON.
            if (response.Content.Headers.ContentType?.MediaType == XmlMediaType)
            {
                errorResult = ParseXmlErrorResult(errorContent);
            }
            else
            {
                errorResult = JsonSerializer.Deserialize<ErrorResult?>(errorContent);
            }

            throw new RegistryException(
                $"Response status code does not indicate success: {response.StatusCode}. See {nameof(RegistryException.Errors)} property for more detail. ({response.ReasonPhrase})")
            {
                Errors = errorResult?.Errors ?? Enumerable.Empty<Error>(),
                StatusCode = response.StatusCode
            };
        }
        response.EnsureSuccessStatusCode();

        return response;
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

    internal static T GetResult<T>(HttpResponseMessage response, string content) =>
        JsonSerializer.Deserialize<T>(content) ?? throw new JsonException($"Unable to deserialize the content:{Environment.NewLine}{content}");

    internal static string? GetNextLinkUrl(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Link", out IEnumerable<string>? linkValues))
        {
            HttpLink? nextLink = linkValues
                .Select(linkValue =>
                {
                    if (HttpLink.TryParse(linkValue, out HttpLink? httpLink))
                    {
                        return httpLink;
                    }

                    return null;
                })
                .FirstOrDefault(link => link?.Relationship == "next") ??
                    throw new InvalidOperationException($"Unable to parse link header '{string.Join(", ", linkValues.ToArray())}'");
            return nextLink.Url;
        }

        return null;
    }

    public void Dispose()
    {
        if (this.disposeHttpClient)
        {
            this.HttpClient.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}
