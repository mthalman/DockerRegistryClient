using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Valleysoft.DockerRegistry.Models
{
    public class Error
    {
        [JsonProperty("code")]
        public string? Code { get; set; }

        [JsonProperty("message")]
        public string? Message { get; set; }

        [JsonProperty("detail")]
        public JToken? Detail { get; set; }
    }
}
