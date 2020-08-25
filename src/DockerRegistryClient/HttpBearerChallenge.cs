using System;
using System.Text.RegularExpressions;

namespace DockerRegistry
{
    internal class HttpBearerChallenge
    {
        private const string ServiceParameter = "service";
        private const string ScopeParameter = "scope";
        private const string RealmParameter = "realm";
        public const string Bearer = "Bearer";

        private static readonly Regex BearerRegex = new Regex(
            $"(realm=\"(?<{RealmParameter}>.+?)\"|service=\"(?<{ServiceParameter}>.+?)\"|scope=\"(?<{ScopeParameter}>.+?)\")");

        public string Realm { get; }
        public string Service { get; }
        public string Scope { get; }

        public HttpBearerChallenge(string realm, string service, string scope)
        {
            this.Realm = realm;
            this.Service = service;
            this.Scope = scope;
        }

        public static HttpBearerChallenge Parse(string challenge)
        {
            if (!ValidateChallenge(challenge))
            {
                return null;
            }

            var matches = BearerRegex.Matches(challenge);

            string realm = null;
            string service = null;
            string scope = null;

            foreach (Match match in matches)
            {
                realm = realm ?? GetGroupValue(match, RealmParameter);
                service = service ?? GetGroupValue(match, ServiceParameter);
                scope = scope ?? GetGroupValue(match, ScopeParameter);
            }

            return new HttpBearerChallenge(realm, service, scope);
        }

        private static string GetGroupValue(Match match, string groupName)
        {
            Group group = match.Groups[groupName];
            return group.Success ? group.Value : null;
        }

        private static bool ValidateChallenge(string challenge)
        {
            if (String.IsNullOrEmpty(challenge) && BearerRegex.IsMatch(challenge))
            {
                return false;
            }

            return true;
        }
    }
}
