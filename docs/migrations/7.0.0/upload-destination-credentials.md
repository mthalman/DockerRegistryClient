# Scope cached upload authorization to its originating destination

**Version introduced:** 7.0.0

Cached upload authorization is reused only for the origin where it was
established. Applications that reuse upload credentials across origins must
provide authorization appropriate to the destination instead.

## Previous behavior

Cached upload authorization did not record the origin of the request that
established it and could be reused at a different destination.

## New behavior

Cached authorization is reused only at its originating scheme, host, and
effective port. Initialization captures authorization from the effective
response request rather than assuming that the original request supplied it.
This also applies when an injected HTTP client supplies the effective request.

Relative and absolute HTTP(S) upload locations remain supported, including
external locations. The credential provider is not invoked for upload requests
to another origin. Ordinary redirect handling clears `Authorization` and does
not follow HTTPS-to-HTTP redirects. There is no additional upload transport
policy.

## Type of breaking change

This changes upload behavior without changing public method signatures.
Cross-origin reuse of cached upload authorization is no longer supported.

## Reason for change

Support OCI upload offloading without reusing a registry token at a different
origin.

## Recommended action

Prefer HTTPS presigned URLs for offloaded uploads. Pass returned upload locations
unchanged to subsequent upload operations, with the context for that session.
Do not rely on a context from another registry to provide authentication.

Shared default headers and custom headers remain caller-controlled; they are
not subject to the cached-authorization scope. An injected `HttpClient` remains
responsible for its own redirect policy.

## Affected APIs

`IBlobOperations.BeginUploadAsync`, `GetUploadAsync`, `DeleteUploadAsync`,
`SendUploadStreamAsync`, and `EndUploadAsync`; `BlobUploadContext`; and the
convenience `UploadAsync` and `RegistryClient.CopyAsync` workflows.
