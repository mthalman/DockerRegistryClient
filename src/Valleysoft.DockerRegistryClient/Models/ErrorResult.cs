using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// Represents the error envelope returned by a registry.
/// </summary>
/// <remarks>
/// See the <see href="https://github.com/opencontainers/distribution-spec/blob/main/spec.md#error-codes">OCI Distribution Specification error codes</see>.
/// </remarks>
public class ErrorResult
{
    /// <summary>
    /// Gets or sets the reported registry errors.
    /// </summary>
    [JsonPropertyName("errors")]
    public Error[] Errors { get; set; } = Array.Empty<Error>();
}
