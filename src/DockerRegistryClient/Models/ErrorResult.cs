using System;
using Newtonsoft.Json;

namespace DockerRegistry.Models
{
    public class ErrorResult
    {
        [JsonProperty("errors")]
        public Error[] Errors { get; set; } = Array.Empty<Error>();
    }
}
