# Blob Operations

Access blob operations via `client.Blobs`.

All snippets below use the following setup code:

```csharp
using System.Security.Cryptography;
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Credentials;
using Valleysoft.DockerRegistryClient.Models.Images;
using Valleysoft.DockerRegistryClient.Models.Manifests;

BasicAuthenticationCredentials credentials = new("username", "password");
using RegistryClient client = new("myregistry.example.com", credentials);
const string repository = "myrepo";
```

## Upload a blob

`UploadAsync` initializes and completes a blob upload in one call. Its
`digest` argument identifies the complete uploaded content.

```csharp
using Stream data = File.OpenRead("layer.tar.gz");
string digest = $"sha256:{Convert.ToHexString(await SHA256.HashDataAsync(data)).ToLowerInvariant()}";
data.Position = 0;

BlobUploadResult result = await client.Blobs.UploadAsync(
    repository,
    data,
    digest);

Console.WriteLine(result.Digest);
```

`UploadAsync`, `SendUploadStreamAsync`, and stream-taking `EndUploadAsync`
consume from the stream's current position and dispose the supplied stream.
The digest must match those bytes.

The [chunked upload APIs](#upload-a-blob-in-chunks) provide control over
individual requests.

## Download a blob

`GetAsync` buffers the complete response before returning a `Stream`.
The caller owns the returned stream. `GetRangeAsync` instead streams the
response after headers arrive.

The `digest` argument identifies a blob, not its parent manifest. Image
manifests expose blob digests in their config and layer descriptors.
The example below reads the first layer of a single-image manifest and
overwrites `layer.tar.gz`:

```csharp
ManifestInfo info = await client.Manifests.GetAsync(repository, "latest");
IImageManifest imageManifest = info.Manifest as IImageManifest
    ?? throw new InvalidOperationException("Select a single-image manifest before downloading a layer.");
string blobDigest = imageManifest.Layers.First().Digest;
using Stream blobStream = await client.Blobs.GetAsync(repository, blobDigest);

using FileStream file = File.Create("layer.tar.gz");
await blobStream.CopyToAsync(file);
```

## Download part of a blob

`GetRangeAsync` requests a byte range from `blobDigest`, the digest of the
target blob. `offset` is zero-based. `length` is a positive byte count;
omitting it requests the remaining bytes.

```csharp
BlobDownloadResult download = await client.Blobs.GetRangeAsync(
    repository,
    blobDigest,
    offset: 1024,
    length: 4096);

using Stream content = download.Content;
if (!download.IsRangeHonored)
{
    // The registry returned the complete blob. Choose whether to use or discard it.
    throw new InvalidOperationException("The registry did not honor the requested range.");
}

using FileStream file = File.Create("layer-part.bin");
await content.CopyToAsync(file);

Console.WriteLine($"Received bytes {download.RangeStart}-{download.RangeEnd}");
Console.WriteLine($"Reported blob length: {download.TotalLength}");
```

`RangeStart` and `RangeEnd` are inclusive and describe the bytes that the
registry returned. A registry can return only part of the requested range, so
do not assume that the response starts at `offset` or contains `length` bytes.
`TotalLength` is `null` when the registry does not report the complete blob
length.

A registry can also ignore a range request and return `200 OK`. In that case,
`IsRangeHonored` is `false`, and `Content` contains the complete response body.
An unsatisfiable range throws a `RegistryException` with `StatusCode` set to
`HttpStatusCode.RequestedRangeNotSatisfiable`.

For resumed downloads, appending is valid only when `IsRangeHonored` is true
and `RangeStart` equals the existing file length. The existing file must
contain the prefix of the same blob:

```csharp
const string destination = "layer.tar.gz";
long offset = File.Exists(destination) ? new FileInfo(destination).Length : 0;

BlobDownloadResult download = await client.Blobs.GetRangeAsync(
    repository,
    blobDigest,
    offset);

using Stream content = download.Content;
if (!download.IsRangeHonored || download.RangeStart != offset)
{
    throw new InvalidOperationException(
        "The registry did not return data from the requested resume offset.");
}

using FileStream file = new(
    destination,
    FileMode.Append,
    FileAccess.Write,
    FileShare.None);
await content.CopyToAsync(file);
```

Short partial responses can require additional requests. `TotalLength`, when
available, indicates the expected assembled length; otherwise completion
requires a length from another trusted source.

The client does not persist checkpoints, retry interrupted downloads, or
verify assembled content. Digest verification of the assembled blob is the
caller's responsibility.

## Read an image configuration

`GetImageAsync` returns an `Image` deserialized from a Docker or OCI
configuration blob. Its digest is available from `IImageManifest.Config`:

```csharp
string configDigest = imageManifest.Config?.Digest
    ?? throw new InvalidOperationException("The manifest does not contain an image configuration.");
Image image = await client.Blobs.GetImageAsync(
    repository,
    configDigest);
```

`GetImageAsync` throws `System.Text.Json.JsonException` when the JSON cannot
be deserialized into the image model. It does not validate the complete
Docker or OCI configuration schema; absent fields can retain model defaults.

## Check whether a blob exists

```csharp
bool exists = await client.Blobs.ExistsAsync(repository, blobDigest);
```

`ExistsAsync` returns `true` for successful HTTP responses and `false` only for
`404 Not Found`. Other non-success responses throw `RegistryException`; see
[Error Handling](error-handling.md).

## Delete a blob

`DeleteAsync` deletes the blob identified by `blobDigest`. Deletion requires
registry support and authorization. Deleting a referenced blob can make
images that use it unavailable.

```csharp
await client.Blobs.DeleteAsync(repository, blobDigest);
```

## Upload a blob in chunks

The primitive upload APIs operate on an upload session:

| Method | Behavior |
| --- | --- |
| `BeginUploadAsync` | Creates a session and returns its location, ID, and authorization context |
| `SendUploadStreamAsync` | Appends a chunk and returns the updated location and offset |
| `EndUploadAsync` | Completes the session with the complete blob digest and an optional final chunk |

`BlobUploadContext` retains the authorization established during
initialization. Subsequent chunk and completion requests take that session's
context and the latest returned `Location`.

The registry may offload uploads to an external HTTP or HTTPS destination, including a
different host or port. The client trusts that destination with blob contents,
but reuses cached upload authorization only at its originating scheme, host, and
effective port. The context records the authorization on the effective
initialization request, including when a custom HTTP pipeline redirects it.

Upload-session locations are accepted without a separate HTTPS or network-address
policy. The normal redirect rules still apply: an HTTPS request does not follow
an HTTP redirect. A session `Location` is not a redirect and can select an HTTP
destination. Use a trusted registry and HTTPS locations to protect blob contents.
Completed-blob locations are returned as metadata, not followed.

Offloaded uploads should use presigned URLs: the built-in OAuth handler does
not exchange tokens for challenges from external storage. See
[custom credentials](authentication.md#provide-custom-credentials) for
authorization scoping and caller-controlled headers, and
[HTTP pipeline ownership](authentication.md#control-the-http-pipeline) for
injected-client responsibilities.

### Begin the upload

`BeginUploadAsync` takes the destination repository name:

```csharp
BlobUploadInitializationResult init = await client.Blobs.BeginUploadAsync(repository);
```

The result contains the upload `Location`, `UploadId`, and `UploadContext`.

`Location` is an opaque continuation link that can contain server state.
See [server-issued links](authentication.md#server-issued-links)
for continuation-link and credential behavior.

### Send chunks

`SendUploadStreamAsync` appends the supplied stream's content. Each response
provides the location for the next request:

```csharp
using Stream chunk1 = File.OpenRead("chunk1.bin");
BlobUploadStreamResult result1 = await client.Blobs.SendUploadStreamAsync(
    init.Location, chunk1, init.UploadContext);

using Stream chunk2 = File.OpenRead("chunk2.bin");
BlobUploadStreamResult result2 = await client.Blobs.SendUploadStreamAsync(
    result1.Location, chunk2, init.UploadContext);
```

Each `BlobUploadStreamResult` contains an updated `Location`, the `UploadId`,
and the zero-based inclusive `RangeOffset` of the uploaded bytes.

### Complete the upload

`EndUploadAsync` takes `digest`, the digest of all uploaded chunks in order,
not an individual chunk:

```csharp
BlobUploadResult uploadResult = await client.Blobs.EndUploadAsync(
    result2.Location, digest, init.UploadContext);
```

The optional stream argument appends a final chunk before completion.
This variant completes the session after `result1` with a second chunk:

```csharp
using Stream finalChunk = File.OpenRead("chunk2.bin");
BlobUploadResult uploadResult = await client.Blobs.EndUploadAsync(
    result1.Location, digest, init.UploadContext, finalChunk);
```

### Complete an upload without separate chunk requests

`EndUploadAsync` also accepts the complete blob without prior chunk requests.
`UploadAsync` wraps this initialization-and-completion pattern:

```csharp
using Stream data = File.OpenRead("small-blob.bin");
string digest = $"sha256:{Convert.ToHexString(await SHA256.HashDataAsync(data)).ToLowerInvariant()}";
data.Position = 0;
BlobUploadInitializationResult init = await client.Blobs.BeginUploadAsync(repository);
BlobUploadResult result = await client.Blobs.EndUploadAsync(
    init.Location, digest, init.UploadContext, data);
```

## Inspect or cancel an upload

`GetUploadAsync` returns the session's current ID and offset.
`DeleteUploadAsync` cancels an in-progress session. Both take the current
upload location:

```csharp
BlobUpload upload = await client.Blobs.GetUploadAsync(init.Location);
```

Cancellation:

```csharp
await client.Blobs.DeleteUploadAsync(init.Location);
```
