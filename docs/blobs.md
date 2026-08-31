# Blob Operations

Access blob operations via `client.Blobs`.

## Upload a blob

Use `UploadAsync` for a one-call convenience upload. Supply the digest of the
complete stream:

```csharp
using Valleysoft.DockerRegistryClient;

using RegistryClient client = new("myregistry.example.com", credentials);
using Stream data = File.OpenRead("layer.tar.gz");

BlobUploadResult result = await client.Blobs.UploadAsync(
    "myrepo",
    data,
    "sha256:abc123...");

Console.WriteLine(result.Digest);
```

Use the primitive upload operations described in
[Upload a blob in chunks](#upload-a-blob-in-chunks) when you need to retry
individual chunks.

## Download a blob

`GetAsync` returns a `Stream`. The caller is responsible for disposing it:

```csharp
using Valleysoft.DockerRegistryClient;

using RegistryClient client = new("mcr.microsoft.com");
using Stream blobStream = await client.Blobs.GetAsync("dotnet/sdk", "sha256:abc123...");

using FileStream file = File.Create("layer.tar.gz");
await blobStream.CopyToAsync(file);
```

## Read an image configuration

`GetImageAsync` downloads and deserializes a blob that contains a Docker or OCI
image configuration:

```csharp
using System.Text.Json;
using Valleysoft.DockerRegistryClient.Models.Images;

Image image = await client.Blobs.GetImageAsync(
    "dotnet/sdk",
    "sha256:abc123...");
```

`GetImageAsync` throws `JsonException` when the blob is not a valid image
configuration.

## Check whether a blob exists

```csharp
bool exists = await client.Blobs.ExistsAsync("myrepo", "sha256:abc123...");
```

`ExistsAsync` returns `false` for any non-success HTTP status.

## Delete a blob

```csharp
await client.Blobs.DeleteAsync("myrepo", "sha256:abc123...");
```

The registry must allow blob deletion.

## Upload a blob in chunks

Chunked uploads use a multi-request workflow. The `BlobUploadContext` preserves
the authorization header established by `BeginUploadAsync`; pass the same
context to every subsequent request in that upload session.

### 1. Begin the upload

```csharp
BlobUploadInitializationResult init = await client.Blobs.BeginUploadAsync("myrepo");
```

The result contains the upload `Location`, `UploadId`, and `UploadContext`.

### 2. Send chunks

Pass the `Location` from each response to the next request:

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

### 3. Complete the upload

Finalize the upload with the digest of the complete blob:

```csharp
BlobUploadResult uploadResult = await client.Blobs.EndUploadAsync(
    result2.Location, "sha256:abc123...", init.UploadContext);
```

To send the final chunk and complete the upload in one request, pass a stream
to `EndUploadAsync`:

```csharp
using Stream finalChunk = File.OpenRead("final.bin");
BlobUploadResult uploadResult = await client.Blobs.EndUploadAsync(
    result1.Location, "sha256:abc123...", init.UploadContext, finalChunk);
```

### Complete an upload without separate chunk requests

You can skip `SendUploadStreamAsync` and pass the complete stream to
`EndUploadAsync`. Prefer `UploadAsync` unless you need the initialization
result:

```csharp
BlobUploadInitializationResult init = await client.Blobs.BeginUploadAsync("myrepo");

using Stream data = File.OpenRead("small-blob.bin");
BlobUploadResult result = await client.Blobs.EndUploadAsync(
    init.Location, "sha256:abc123...", init.UploadContext, data);
```

## Inspect or cancel an upload

Get the server's current offset for an in-progress upload:

```csharp
BlobUpload upload = await client.Blobs.GetUploadAsync(init.Location);
```

Cancel the upload:

```csharp
await client.Blobs.DeleteUploadAsync(init.Location);
```
