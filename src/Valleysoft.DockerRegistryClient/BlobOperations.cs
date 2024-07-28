using System.Net.Http.Headers;
using System.Text.RegularExpressions;

namespace Valleysoft.DockerRegistryClient;

internal class BlobOperations : IBlobOperations
{
    private const string RangeHeader = "Range";
    private const string UuidHeader = "Docker-Upload-UUID";

    public RegistryClient Client { get; }

    public BlobOperations(RegistryClient client)
    {
        this.Client = client;
    }

    /// <summary>
    /// Returns the data stream of the specified blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<Stream> GetAsync(string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, $"{this.Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/{digest}");
        HttpResponseMessage response = await this.Client.SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        HttpOperationResponse<Stream> streamContentResponse = await OperationsHelper.HandleNotFoundErrorAsync(
            "Blob not found.",
            () => RegistryClient.GetStreamContentAsync(request, response)).ConfigureAwait(false);

        return new BlobStream(streamContentResponse);
    }

    /// <summary>
    /// Checks whether the specified blob exists.
    /// </summary>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>true if the blob exists; otherwise, false.</returns>
    public async Task<bool> ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Head, $"{this.Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/{digest}");
        return await this.Client.SendExistsRequestAsync(request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Deletes the specified blob.
    /// </summary>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task DeleteAsync(string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, $"{this.Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/{digest}");
        await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns info about an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task<BlobUpload> GetUploadAsync(string uploadLocation, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, new Uri(Client.BaseUri, uploadLocation));
        using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        return new BlobUpload(GetUploadId(response), GetRangeOffset(response));
    }

    /// <summary>
    /// Deletes an in-progress blob upload.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public async Task DeleteUploadAsync(string uploadLocation, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Delete, new Uri(Client.BaseUri, uploadLocation));
        await this.Client.SendRequestAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Begins a blob upload operation.
    /// </summary>
    /// <param name="repositoryName">Name of the repository to upload the blob to.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// This method estabilishes authentication for the upload process and generates the upload UUID.
    /// This primitive method can be used over the <see cref="UploadAsync"/> convenience method when you need to have greater control over
    /// the upload process, such as breaking up the upload into multiple requests to allow for smaller chunks of the data to be retried if the
    /// upload fails.
    /// </remarks>
    public async Task<BlobUploadInitializationResult> BeginUploadAsync(string repositoryName, CancellationToken cancellationToken = default)
    {
        using HttpRequestMessage request = new(HttpMethod.Post,
            $"{Client.BaseUri.AbsoluteUri}v2/{repositoryName}/blobs/uploads/");

        HttpResponseMessage response = await this.Client.SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);

        // Cache the authorization header for subsequent requests. This avoids re-requesting bearer tokens
        // for each request within a given client instance. This is particularly important for upload scenarios
        // where a bunch of data may be sent in the initial request only to be rejected for auth and forced to
        // upload again.
        return new BlobUploadInitializationResult(GetLocation(response), GetUploadId(response), new BlobUploadContext(request.Headers.Authorization));
    }

    /// <summary>
    /// Sends an upload stream as a chunk of the overall data to be uploaded for the blob.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="stream">Data stream to upload as a chunk of the overall blob.</param>
    /// <param name="uploadContext">The <see cref="BlobUploadContext"/> that was returned in the result of <see cref="BeginUploadAsync"/>.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// This method is typically used when needing to break up the overall blob data into smaller chunks. It would be called for each
    /// chunk of the data. The final chunk of the data can be sent via the <see cref="EndUploadAsync"/> method.
    /// This primitive method can be used over the <see cref="UploadAsync"/> convenience method when you need to have greater control over
    /// the upload process, such as breaking up the upload into multiple requests to allow for smaller chunks of the data to be retried if the
    /// upload fails.
    /// </remarks>
    public async Task<BlobUploadStreamResult> SendUploadStreamAsync(string uploadLocation, Stream stream, BlobUploadContext uploadContext, CancellationToken cancellationToken = default)
    {
#if NETSTANDARD2_0
        HttpMethod patchMethod = new("PATCH");
#else
        HttpMethod patchMethod = HttpMethod.Patch;
#endif
        using HttpRequestMessage request = new(patchMethod, new Uri(Client.BaseUri, uploadLocation))
        {
            Content = CreateStreamContent(stream)
        };
        // Reuse the Auth header from the initial upload to avoid re-authenticating and wasting upload time in OAuth flow
        request.Headers.Authorization = uploadContext.Authorization;

        using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new BlobUploadStreamResult(GetLocation(response), GetUploadId(response), GetRangeOffset(response));
    }

    /// <summary>
    /// Completes the upload of a blob.
    /// </summary>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="digest">Digest of the complete data for the blob.</param>
    /// <param name="uploadContext">The <see cref="BlobUploadContext"/> that was returned in the result of <see cref="BeginUploadAsync"/>.</param>
    /// <param name="stream">Data stream to upload as a chunk of the overall blob.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// This method allows you to optionally provide the final (or only) stream of data for the blob. If the stream isn't provided in this method,
    /// it needs to have been done so via the <see cref="SendUploadStreamAsync"/> method.
    /// This primitive method can be used over the <see cref="UploadAsync"/> convenience method when you need to have greater control over
    /// the upload process, such as breaking up the upload into multiple requests to allow for smaller chunks of the data to be retried if the
    /// upload fails.
    /// </remarks>
    public async Task<BlobUploadResult> EndUploadAsync(string uploadLocation, string digest, BlobUploadContext uploadContext, Stream? stream = null, CancellationToken cancellationToken = default)
    {
        Uri uri = new(Client.BaseUri, uploadLocation);
        char uriAppendChar = string.IsNullOrEmpty(uri.Query) ? '?' : '&';
        uri = new Uri($"{uri}{uriAppendChar}digest={digest}");

        using HttpRequestMessage request = new(HttpMethod.Put, uri)
        {
            Content = stream is null ? null : CreateStreamContent(stream)
        };
        // Reuse the Auth header from the initial upload to avoid re-authenticating and wasting upload time in OAuth flow
        request.Headers.Authorization = uploadContext.Authorization;

        using HttpResponseMessage response = await this.Client.SendRequestCoreAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
        return new BlobUploadResult(GetLocation(response), GetDigest(response));
    }

    private static string GetLocation(HttpResponseMessage responseMsg)
    {
        string? location = (responseMsg.Headers.Location?.ToString()) ?? throw new InvalidOperationException("Location header not set.");
        return location;
    }

    private static Guid GetUploadId(HttpResponseMessage responseMsg)
    {
        string uuid = responseMsg.Headers.GetValues(UuidHeader).First();
        if (!Guid.TryParse(uuid, out Guid id))
        {
            throw new Exception($"Unable to parse {UuidHeader} value '{uuid}'. Expected a valid GUID.");
        }

        return id;
    }

    private static string GetDigest(HttpResponseMessage responseMsg) =>
        responseMsg.Headers.GetValues("Docker-Content-Digest").First();

    private static long GetRangeOffset(HttpResponseMessage responseMsg)
    {
        string range = responseMsg.Headers.GetValues(RangeHeader).First();
        Match match = Regex.Match(range, @"0-(?<offset>\d+)");
        if (!match.Success)
        {
            throw new Exception($"Unable to parse {RangeHeader} value '{range}'. Expected '0-<offset>'");
        }

        return long.Parse(match.Groups["offset"].Value);
    }

    private class BlobStream : Stream
    {
        private readonly HttpOperationResponse<Stream> response;

        internal BlobStream(HttpOperationResponse<Stream> response)
        {
            this.response = response;
        }

        public override bool CanRead => this.response.Body.CanRead;

        public override bool CanSeek => this.response.Body.CanSeek;

        public override bool CanWrite => this.response.Body.CanWrite;

        public override bool CanTimeout => this.response.Body.CanTimeout;

        public override int ReadTimeout
        {
            get => this.response.Body.ReadTimeout;
            set => this.response.Body.ReadTimeout = value;
        }

        public override int WriteTimeout
        {
            get => this.response.Body.WriteTimeout;
            set => this.response.Body.WriteTimeout = value;
        }

        public override long Length => this.response.Body.Length;

        public override long Position
        {
            get => this.response.Body.Position;
            set => this.response.Body.Position = value;
        }

        public override void Flush() => this.response.Body.Flush();

        public override int Read(byte[] buffer, int offset, int count) => this.response.Body.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) => this.response.Body.Seek(offset, origin);

        public override void SetLength(long value) => this.response.Body.SetLength(value);

        public override void Write(byte[] buffer, int offset, int count) => this.response.Body.Write(buffer, offset, count);

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            this.response.Body.BeginRead(buffer, offset, count, callback, state);

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback? callback, object? state) =>
            this.response.Body.BeginWrite(buffer, offset, count, callback, state);

        public override Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken) =>
            this.response.Body.CopyToAsync(destination, bufferSize, cancellationToken);

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.response.Dispose();
            }

            base.Dispose(disposing);
        }

        public override int EndRead(IAsyncResult asyncResult) =>
            this.response.Body.EndRead(asyncResult);

        public override void EndWrite(IAsyncResult asyncResult) =>
            this.response.Body.EndWrite(asyncResult);

        public override Task FlushAsync(CancellationToken cancellationToken) =>
            this.response.Body.FlushAsync(cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.response.Body.ReadAsync(buffer, offset, count, cancellationToken);

        public override int ReadByte() =>
            this.response.Body.ReadByte();

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            this.response.Body.WriteAsync(buffer, offset, count, cancellationToken);

        public override void WriteByte(byte value) =>
            this.response.Body.WriteByte(value);

#if NET6_0_OR_GREATER
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            this.response.Body.ReadAsync(buffer, cancellationToken);

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
            this.response.Body.WriteAsync(buffer, cancellationToken);
#endif
    }

    private static StreamContent CreateStreamContent(Stream stream)
    {
        StreamContent streamContent = new(stream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        return streamContent;
    }
}
