using System;
using System.Collections.Generic;

namespace DockerRegistry
{
    internal class HttpBearerChallenge
    {
        private const string ServiceParameter = "service";
        private const string ScopeParameter = "scope";
        private const string RealmParameter = "realm";
        public const string Bearer = "Bearer";

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

            var parameters = GetChallengeParameters(challenge);
            string realm = null;
            string service = null;
            string scope = null;

            if (parameters.ContainsKey(RealmParameter))
            {
                realm = parameters[RealmParameter];
            }

            if (parameters.ContainsKey(ServiceParameter))
            {
                service = parameters[ServiceParameter];
            }

            if (parameters.ContainsKey(ScopeParameter))
            {
                scope = parameters[ScopeParameter];
            }

            return new HttpBearerChallenge(realm, service, scope);
        }

        private static bool ValidateChallenge(string challenge)
        {
            if (String.IsNullOrEmpty(challenge))
            {
                return false;
            }

            return true;
        }

        private static Dictionary<string, string> GetChallengeParameters(string challenge)
        {
            challenge = challenge.Trim();

            Dictionary<string, string> parameters = new Dictionary<string, string>();
            string[] keyValuePairs = challenge.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

            if (keyValuePairs != null && keyValuePairs.Length > 0)
            {
                for (int i = 0; i < keyValuePairs.Length; i++)
                {
                    string[] keyValuePair = keyValuePairs[i].Split('=');

                    if (keyValuePair.Length == 2)
                    {
                        string key = keyValuePair[0].Trim().Trim('"');
                        string value = keyValuePair[1].Trim().Trim('"');

                        if (!string.IsNullOrEmpty(key))
                        {
                            parameters[key] = value;
                        }
                    }
                }
            }

            return parameters;
        }
    }
}
