# Error Handling

The client throws `RegistryException` when the registry returns an error response.

## RegistryException

| Property | Type | Description |
|---|---|---|
| `StatusCode` | `HttpStatusCode?` | The HTTP status code from the response |
| `Errors` | `IEnumerable<Error>` | Structured error details from the registry |

Each `Error` has:
- `Code` — a registry error code (e.g., `"MANIFEST_UNKNOWN"`, `"UNAUTHORIZED"`)
- `Message` — a human-readable error description

## Example

```csharp
using Valleysoft.DockerRegistryClient;

try
{
    ManifestInfo info = await client.Manifests.GetAsync("myrepo", "nonexistent-tag");
}
catch (RegistryException ex)
{
    Console.WriteLine($"HTTP {ex.StatusCode}");
    foreach (Error error in ex.Errors)
    {
        Console.WriteLine($"  {error.Code}: {error.Message}");
    }
}
```
