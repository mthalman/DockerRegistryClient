using Microsoft.Rest;
using Microsoft.Rest.Serialization;
using Newtonsoft.Json;
using System.Text.RegularExpressions;
using Valleysoft.DockerRegistryClient.Models;

namespace Valleysoft.DockerRegistryClient;

/// <summary>
/// Extension methods for the <see cref="IBlobOperations"/> interface.
/// </summary>
public static class BlobOperationsExtensions
{
    private const string RangeHeader = "Range";
    private const string UuidHeader = "Docker-Upload-UUID";

    /// <summary>
    /// Returns the data stream of the specified blob.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public static async Task<Stream> GetAsync(this IBlobOperations operations, string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse<Stream> response =
            await operations.GetWithHttpMessagesAsync(repositoryName, digest, cancellationToken).ConfigureAwait(false);
        return new BlobStream(response);
    }

    /// <summary>
    /// Checks whether the specified blob exists.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <returns>true if the blob exists; otherwise, false.</returns>
    public static async Task<bool> ExistsAsync(this IBlobOperations operations, string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse<bool> response =
            await operations.ExistsWithHttpMessagesAsync(repositoryName, digest, cancellationToken).ConfigureAwait(false);
        return response.Body;
    }

    /// <summary>
    /// Deletes the specified blob.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public static async Task DeleteAsync(this IBlobOperations operations, string repositoryName, string digest, CancellationToken cancellationToken = default) =>
        await operations.DeleteWithHttpMessagesAsync(repositoryName, digest, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Returns the object model of a blob that represents an image config (e.g. mediaType of application/vnd.docker.container.image.v1+json or application/vnd.oci.image.config.v1+json).
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository the blob belongs to.</param>
    /// <param name="digest">Digest of the blob (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <exception cref="JsonSerializationException">Unable to deserialize the data to the object model.</exception>
    public static async Task<Image> GetImageAsync(this IBlobOperations operations, string repositoryName, string digest, CancellationToken cancellationToken = default)
    {
        using Stream blob = await operations.GetAsync(repositoryName, digest, cancellationToken);
        using StreamReader reader = new(blob);
        string content = await reader.ReadToEndAsync();
        try
        {
            return SafeJsonConvert.DeserializeObject<Image>(content);
        }
        catch (JsonReaderException e)
        {
            throw new JsonSerializationException(
                "The result could not be deserialized into an image model. Verify the digest represents an image config and not a layer.", e);
        }
    }

    /// <summary>
    /// Returns info about an in-progress blob upload.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public static async Task<BlobUpload> GetUploadAsync(this IBlobOperations operations, string uploadLocation, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse response =
            await operations.GetUploadWithHttpMessagesAsync(uploadLocation, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage responseMsg = response.Response;

        return new BlobUpload(GetUploadId(responseMsg), GetRangeOffset(responseMsg));
    }

    /// <summary>
    /// Deletes an in-progress blob upload.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="uploadLocation">The blob location being targeted. This value can be retrieve from the most recently executed result of the <see cref="BeginUploadAsync"/> or <see cref="SendUploadStreamAsync"/> methods.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    public static async Task DeleteUploadAsync(this IBlobOperations operations, string uploadLocation, CancellationToken cancellationToken = default) =>
        await operations.DeleteUploadWithHttpMessagesAsync(uploadLocation, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Uploads a blob to the registry.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository to upload the blob to.</param>
    /// <param name="stream">Data stream to upload as the blob.</param>
    /// <param name="digest">Digest of the data stream (e.g. "sha256:&lt;value&gt;").</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// This is a convenience method that uses the more primitive <see cref="BeginUploadAsync"/> and <see cref="EndUploadAsync"/> methods.
    /// </remarks>
    public static async Task<BlobUploadResult> UploadAsync(this IBlobOperations operations, string repositoryName, Stream stream, string digest, CancellationToken cancellationToken = default)
    {
        BlobUploadInitializationResult result = await operations.BeginUploadAsync(repositoryName, cancellationToken).ConfigureAwait(false);
        return await operations.EndUploadAsync(result.Location, digest, result.UploadContext, stream, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Begins a blob upload operation.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
    /// <param name="repositoryName">Name of the repository to upload the blob to.</param>
    /// <param name="cancellationToken">Propagates notification that the operation should be canceled.</param>
    /// <remarks>
    /// This method estabilishes authentication for the upload process and generates the upload UUID.
    /// This primitive method can be used over the <see cref="UploadAsync"/> convenience method when you need to have greater control over
    /// the upload process, such as breaking up the upload into multiple requests to allow for smaller chunks of the data to be retried if the
    /// upload fails.
    /// </remarks>
    public static async Task<BlobUploadInitializationResult> BeginUploadAsync(this IBlobOperations operations, string repositoryName, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse<BlobUploadContext> response =
            await operations.BeginUploadWithHttpMessagesAsync(repositoryName, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage responseMsg = response.Response;

        return new BlobUploadInitializationResult(GetLocation(responseMsg), GetUploadId(responseMsg), response.Body);
    }

    /// <summary>
    /// Sends an upload stream as a chunk of the overall data to be uploaded for the blob.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
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
    public static async Task<BlobUploadStreamResult> SendUploadStreamAsync(this IBlobOperations operations, string uploadLocation, Stream stream, BlobUploadContext uploadContext, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse response =
            await operations.SendUploadStreamWithHttpMessagesAsync(uploadLocation, stream, uploadContext, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage responseMsg = response.Response;

        return new BlobUploadStreamResult(GetLocation(responseMsg), GetUploadId(responseMsg), GetRangeOffset(responseMsg));
    }

    /// <summary>
    /// Completes the upload of a blob.
    /// </summary>
    /// <param name="operations">Provider of the blob operations.</param>
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
    public static async Task<BlobUploadResult> EndUploadAsync(this IBlobOperations operations, string uploadLocation, string digest, BlobUploadContext uploadContext, Stream? stream = null, CancellationToken cancellationToken = default)
    {
        HttpOperationResponse response =
            await operations.EndUploadWithHttpMessagesAsync(uploadLocation, digest, uploadContext, stream, cancellationToken).ConfigureAwait(false);
        HttpResponseMessage responseMsg = response.Response;

        return new BlobUploadResult(GetLocation(responseMsg), GetDigest(responseMsg));
    }

    private static string GetLocation(HttpResponseMessage responseMsg)
    {
        string? location = responseMsg.Headers.Location?.ToString();
        if (location is null)
        {
            throw new Exception("Location header not set.");
        }

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
}
