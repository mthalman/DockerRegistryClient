using System.Text.Json;
using System.Text.Json.Serialization;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// Represents an error reported by a registry.
/// </summary>
/// <remarks>
/// See the <see href="https://github.com/opencontainers/distribution-spec/blob/main/spec.md#error-codes">OCI Distribution Specification error codes</see>.
/// </remarks>
public class Error
{
    /// <summary>
    /// Gets or sets the registry error code.
    /// </summary>
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    /// <summary>
    /// Gets or sets the human-readable error message.
    /// </summary>
    [JsonPropertyName("message")]
    public string? Message { get; set; }

    /// <summary>
    /// Gets or sets registry-specific structured error details.
    /// </summary>
    [JsonPropertyName("detail")]
    public JsonElement? Detail { get; set; }
}
