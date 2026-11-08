# Error Handling

Most operations throw `RegistryException` when the registry returns a
non-success HTTP response.

## Handle a registry error

| Property | Type | Description |
| --- | --- | --- |
| `StatusCode` | `HttpStatusCode?` | HTTP status code from the response |
| `Errors` | `IEnumerable<Error>` | Structured error details returned by the registry |

Each `Error` exposes `Code`, `Message`, and an optional JSON `Detail` value.

Credential options are described in [Authentication](authentication.md).

```csharp
using Valleysoft.DockerRegistryClient;
using Valleysoft.DockerRegistryClient.Models;
using Valleysoft.DockerRegistryClient.Models.Manifests;

using RegistryClient client = new("myregistry.example.com");

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
`RegistryException`, such as `Manifest not found.`. The inner exception
retains the original status and structured error details.

## Check whether a resource exists

`client.Blobs.ExistsAsync` and `client.Manifests.ExistsAsync` return `true` for
successful HTTP responses and `false` only for `404 Not Found`.

Other non-success responses, including `401 Unauthorized`, `403 Forbidden`,
`429 Too Many Requests`, and `5xx` server errors, throw `RegistryException`.
The exception's `StatusCode` and `Errors` properties contain the response
status and structured registry errors. Authentication failures, authorization
failures, rate limiting, and registry outages do not mean the resource is missing.

Cancellation and transport failures also propagate to the caller rather than
returning `false`.
