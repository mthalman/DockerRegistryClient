using System;
using Newtonsoft.Json;

namespace Valleysoft.DockerRegistry.Models
{
    internal class OAuthToken
    {
        [JsonProperty("token")]
        public string? Token { get; set; }

        [JsonProperty("access_token")]
        public string? AccessToken { get; set; }

        [JsonProperty("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonProperty("issued_at")]
        public DateTime? IssuedAt { get; set; }
    }
}
