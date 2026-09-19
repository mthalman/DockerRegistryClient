### Distinguish missing resources from failed existence checks

#### Previous behavior

In v6.2.0, the built-in blob and manifest `ExistsAsync` methods returned
`response.IsSuccessStatusCode`. Every unsuccessful HTTP response reaching that
helper returned `false`, not just HTTP 404. Authentication failures, forbidden
access, throttling, and server errors could therefore look like missing
resources. Transport failures and cancellation could already throw.

#### New behavior

[PR #175](https://github.com/mthalman/DockerRegistryClient/pull/175) makes
existence checks return `true` for successful HTTP responses and `false` only
for HTTP 404 Not Found. Other unsuccessful responses reaching the response
handler throw `RegistryException`, including HTTP 401, 403, 429, and 5xx.
The exception exposes the response's `StatusCode` and available structured
`Errors`; an empty response body does not turn a failed check into `false`.

#### Type of breaking change

This is a behavioral break for callers that expect a Boolean result after any
HTTP response or use `false` to trigger uploads, repairs, or other fallback
actions. Method signatures are unchanged; this change does not itself require
source edits to compile or introduce a binary API break.

Custom implementations and test doubles are not automatically changed by the
library's response handler. Update their behavior to match the documented
contract. Higher-level operations that check destination existence, including
`RegistryClientExtensions.CopyAsync`, propagate registry failures instead of
treating them as absence.

#### Reason for change

A failed request cannot establish that a resource is missing. Reporting a
permission failure or registry outage as absence can cause callers to upload,
repair, or copy data based on an incorrect result.

#### Recommended action

Keep resource-absence handling separate from request-failure handling. For
example, an existence summary can return "missing" only after a completed
check returns `false`, while allowing failures to reach the application's
error boundary:

```csharp
using Valleysoft.DockerRegistryClient;

static async Task<string> DescribeBlobAsync(
    IBlobOperations blobs,
    string repositoryName,
    string digest,
    CancellationToken cancellationToken = default)
{
    bool exists = await blobs.ExistsAsync(
        repositoryName, digest, cancellationToken).ConfigureAwait(false);
    return exists ? "exists" : "missing";
}
```

At a boundary that can recover, handle `RegistryException` according to
`StatusCode` and inspect `Errors` for diagnostics. Correct credentials or
permissions for authentication and authorization failures; apply a bounded,
cancellation-aware retry policy only to failures your application considers
transient, such as throttling or selected server errors. Otherwise propagate
the failure. Do not catch every exception and return `false`, and do not
reinterpret cancellation or transport failures as missing resources.

Update tests to distinguish successful responses, HTTP 404, and non-404 errors.
No new catch is needed solely to handle HTTP 404: the built-in `ExistsAsync`
already converts it to `false`.

#### Affected APIs

- `IBlobOperations.ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken = default)`,
  accessed through `RegistryClient.Blobs`.
- `IManifestOperations.ExistsAsync(string repositoryName, string digest, CancellationToken cancellationToken = default)`,
  accessed through `RegistryClient.Manifests`; its `digest` parameter accepts a
  manifest tag or digest.
- Indirect callers such as `RegistryClientExtensions.CopyAsync`.
- Failure details: `RegistryException.StatusCode` and `RegistryException.Errors`.
