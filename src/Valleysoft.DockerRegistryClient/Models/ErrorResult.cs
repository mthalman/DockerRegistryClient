using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

public class ErrorResult
{
    [JsonProperty("errors")]
    public Error[] Errors { get; set; } = Array.Empty<Error>();
}
