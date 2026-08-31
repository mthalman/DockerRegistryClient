# Error Handling

Most operations throw `RegistryException` when the registry returns a
non-success HTTP response.

## Handle a registry error

| Property | Type | Description |
| --- | --- | --- |
| `StatusCode` | `HttpStatusCode?` | HTTP status code from the response |
| `Errors` | `IEnumerable<Error>` | Structured error details returned by the registry |

Each `Error` exposes `Code`, `Message`, and an optional JSON `Detail` value.

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Models;
using Valleysoft.DockerRegistryClient.Models.Manifests;

try
{
    ManifestInfo info = await client.Manifests.GetAsync("myrepo", "nonexistent-tag");
}
catch (RegistryException ex)
{
    RegistryException responseError =
        ex.InnerException as RegistryException ?? ex;

    Console.WriteLine($"HTTP {responseError.StatusCode}");
    foreach (Error error in responseError.Errors)
    {
        Console.WriteLine($"  {error.Code}: {error.Message}");
    }
}
```

Some operations replace a `404 Not Found` exception with a more specific
`RegistryException`, such as `Manifest not found.`. In that case, read the
original status and error details from the inner exception, as the example
does.

## Check existence without an exception

`client.Blobs.ExistsAsync` and `client.Manifests.ExistsAsync` return `false`
instead of throwing for any non-success HTTP status. A `false` result can
therefore mean that the resource is missing or that the registry rejected the
request.
