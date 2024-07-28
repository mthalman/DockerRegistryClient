using System.Net.Http.Headers;

namespace Valleysoft.DockerRegistryClient;

internal class BlobOperations : IBlobOperations
{
    public RegistryClient Client { get; }

    public BlobOperations(RegistryClient client)
    {
        this.Client = client;
    }

    /// <summary>
    /// Returns a <see cref="HttpOperationResponse"/> for a data stream of the specified blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse<Stream>> GetWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, $"{this.Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        return await OperationsHelper.HandleNotFoundErrorAsync(
            "Blob not found.",
            () => RegistryClient.GetStreamContentAsync(request, response)).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns a <see cref="HttpOperationResponse"/> for the result of whether the specified blob exists.
    /// </summary>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse<bool>> ExistsWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Head, $"{this.Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, ignoreUnsuccessfulResponse: true, cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse<bool>(request, response, response.IsSuccessStatusCode);
    }

    /// <summary>
    /// Deletes the specified blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse> DeleteWithHttpMessagesAsync(
        string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, $"{this.Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse(request, response);
    }

    /// <summary>
    /// Returns a <see cref="HttpOperationResponse"/> for an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse> GetUploadWithHttpMessagesAsync(
        string uploadLocation, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Get, new Uri(Client.BaseUri, uploadLocation));
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse(request, response);
    }

    /// <summary>
    /// Deletes an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse> DeleteUploadWithHttpMessagesAsync(
        string uploadLocation, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Delete, new Uri(Client.BaseUri, uploadLocation));
        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse(request, response);
    }

    /// <summary>
    /// Begins a blob upload operation.
    /// </summary>
    /// <param name="repositoryName">Name of the repository to upload the blob to.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse<BlobUploadContext>> BeginUploadWithHttpMessagesAsync(
        string repositoryName, CancellationToken cancellationToken = default)
    {
        HttpRequestMessage request = new(HttpMethod.Post,
            $"{Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/uploads/");

        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        // Cache the authorization header for subsequent requests. This avoids re-requesting bearer tokens
        // for each request within a given client instance. This is particularly important for upload scenarios
        // where a bunch of data may be sent in the initial request only to be rejected for auth and forced to
        // upload again.
        return new HttpOperationResponse<BlobUploadContext>(request, response, new BlobUploadContext(request.Headers.Authorization));
    }

    /// <summary>
    /// Sends an upload stream as a chunk of the overall data to be uploaded for the blob.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="stream">Data stream to upload as a chunk of the overall blob.</param>
    /// <param name="uploadContext">The <see cref="BlobUploadContext"/> that was returned in the result of <see cref="BeginUploadAsync"/>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse> SendUploadStreamWithHttpMessagesAsync(
        string uploadLocation, Stream stream, BlobUploadContext uploadContext, CancellationToken cancellationToken = default)
    {
#if NETSTANDARD2_0
        HttpMethod patchMethod = new("PATCH");
#else
        HttpMethod patchMethod = HttpMethod.Patch;
#endif
        HttpRequestMessage request = new(patchMethod, new Uri(Client.BaseUri, uploadLocation))
        {
            Content = CreateStreamContent(stream)
        };
        // Reuse the Auth header from the initial upload to avoid re-authenticating and wasting upload time in OAuth flow
        request.Headers.Authorization = uploadContext.Authorization;

        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse(request, response);
    }

    /// <summary>
    /// Completes the upload of a blob.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="digest">Digest of the complete data for the blob.</param>
    /// <param name="uploadContext">The <see cref="BlobUploadContext"/> that was returned in the result of <see cref="BeginUploadAsync"/>.</param>
    /// <param name="stream">Data stream to upload as a chunk of the overall blob.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<HttpOperationResponse> EndUploadWithHttpMessagesAsync(
        string uploadLocation, string digest, BlobUploadContext uploadContext, Stream? stream = null, CancellationToken cancellationToken = default)
    {
        Uri uri = new(Client.BaseUri, uploadLocation);
        char uriAppendChar = string.IsNullOrEmpty(uri.Query) ? '?' : '&';
        uri = new Uri($"{uri}{uriAppendChar}digest={digest}");

        HttpRequestMessage request = new(HttpMethod.Put, uri)
        {
            Content = stream is null ? null : CreateStreamContent(stream)
        };
        // Reuse the Auth header from the initial upload to avoid re-authenticating and wasting upload time in OAuth flow
        request.Headers.Authorization = uploadContext.Authorization;

        HttpResponseMessage response = await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new HttpOperationResponse(request, response);
    }

    private static StreamContent CreateStreamContent(Stream stream)
    {
        StreamContent streamContent = new(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return streamContent;
    }
}
