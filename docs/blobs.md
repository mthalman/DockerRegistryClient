# Blob Operations

Access blob operations via `client.Blobs`.

## Downloading a Blob

`GetAsync` returns a `Stream`. The caller is responsible for disposing it:

```csharp
using RegistryClient client = new("mcr.microsoft.com");
using Stream blobStream = await client.Blobs.GetAsync("dotnet/sdk", "sha256:abc123...");

// Copy to a file, deserialize, etc.
using FileStream file = File.Create("layer.tar.gz");
await blobStream.CopyToAsync(file);
```

## Checking Existence

```csharp
bool exists = await client.Blobs.ExistsAsync("myrepo", "sha256:abc123...");
```

## Deleting a Blob

```csharp
await client.Blobs.DeleteAsync("myrepo", "sha256:abc123...");
```

## Uploading a Blob

Blob uploads follow a multi-step workflow. The `BlobUploadContext` carries authentication state across all steps in an upload session.

### 1. Begin the Upload

```csharp
BlobUploadInitializationResult init = await client.Blobs.BeginUploadAsync("myrepo");
```

`init` provides:
- `Location` — the upload URL for subsequent calls
- `UploadId` — a unique identifier for this upload
- `UploadContext` — auth state to pass to subsequent calls

### 2. Send Data (Chunked)

Use `SendUploadStreamAsync` to send data in chunks. Pass the `Location` from the previous result to chain calls:

```csharp
using Stream chunk1 = File.OpenRead("chunk1.bin");
BlobUploadStreamResult result1 = await client.Blobs.SendUploadStreamAsync(
    init.Location, chunk1, init.UploadContext);

using Stream chunk2 = File.OpenRead("chunk2.bin");
BlobUploadStreamResult result2 = await client.Blobs.SendUploadStreamAsync(
    result1.Location, chunk2, init.UploadContext);
```

Each `BlobUploadStreamResult` contains an updated `Location` and `RangeOffset` (inclusive offset of uploaded bytes).

### 3. Complete the Upload

Finalize the upload with `EndUploadAsync`, providing the expected digest:

```csharp
BlobUploadResult uploadResult = await client.Blobs.EndUploadAsync(
    result2.Location, "sha256:abc123...", init.UploadContext);
```

You can optionally pass a final `Stream` to `EndUploadAsync` to send remaining data in the same call:

```csharp
using Stream finalChunk = File.OpenRead("final.bin");
BlobUploadResult uploadResult = await client.Blobs.EndUploadAsync(
    result1.Location, "sha256:abc123...", init.UploadContext, finalChunk);
```

### Single-Chunk Upload

For small blobs, you can skip `SendUploadStreamAsync` and send all data in `EndUploadAsync`:

```csharp
BlobUploadInitializationResult init = await client.Blobs.BeginUploadAsync("myrepo");

using Stream data = File.OpenRead("small-blob.bin");
BlobUploadResult result = await client.Blobs.EndUploadAsync(
    init.Location, "sha256:abc123...", init.UploadContext, data);
```

## Managing Uploads

Check the status of an in-progress upload:

```csharp
BlobUpload upload = await client.Blobs.GetUploadAsync(init.Location);
```

Cancel an in-progress upload:

```csharp
await client.Blobs.DeleteUploadAsync(init.Location);
```
