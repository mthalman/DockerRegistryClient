using Newtonsoft.Json;

namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// The execution parameters which should be used as a base when running a container using the image.
/// </summary>
public class ImageConfig
{
    /// <summary>
    /// The username or UID which is a platform-specific structure that allows specific control over which user the process run as.
    /// </summary>
    [JsonProperty("User")]
    public string? User { get; set; }

    /// <summary>
    /// A set of ports to expose from a container running this image.
    /// </summary>
    [JsonProperty("ExposedPorts")]
    public IDictionary<string, object> ExposedPorts { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Environment variables.
    /// </summary>
    [JsonProperty("Env")]
    public string[] EnvironmentVariables { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A list of arguments to use as the command to execute when the container starts.
    /// </summary>
    [JsonProperty("Entrypoint")]
    public string[] EntrypointArgs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Default arguments to the entrypoint of the container.
    /// </summary>
    [JsonProperty("Cmd")]
    public string[] CommandArgs { get; set; } = Array.Empty<string>();

    /// <summary>
    /// A set of directories describing where the process is likely to write data specific to a container instance.
    /// </summary>
    [JsonProperty("Volumes")]
    public IDictionary<string, object> Volumes { get; set; } = new Dictionary<string, object>();

    /// <summary>
    /// Sets the current working directory of the entrypoint process in the container.
    /// </summary>
    [JsonProperty("WorkingDir")]
    public string? WorkingDir { get; set; }

    /// <summary>
    /// List of labels set to the container
    /// </summary>
    [JsonProperty("Labels")]
    public IDictionary<string, string> Labels { get; set; } = new Dictionary<string, string>();

    /// <summary>
    /// Contains the system call signal that will be sent to the container to exit.
    /// </summary>
    [JsonProperty("StopSignal")]
    public string? StopSignal { get; set; }

    /// <summary>
    /// Indicates that the Entrypoint or Cmd or both, contains only a single element array, that is pre-escaped.
    /// </summary>
    [JsonProperty("ArgsEscaped")]
    public bool ArgsEscaped { get; set; }
}
