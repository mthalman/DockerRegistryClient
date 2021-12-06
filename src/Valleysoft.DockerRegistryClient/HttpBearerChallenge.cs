using System.Text.RegularExpressions;

namespace Valleysoft.DockerRegistryClient;

internal class HttpBearerChallenge
{
    private const string ServiceParameter = "service";
    private const string ScopeParameter = "scope";
    private const string RealmParameter = "realm";
    public const string Bearer = "Bearer";

    private static readonly Regex BearerRegex = new(
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

    public static HttpBearerChallenge Parse(string? challenge)
    {
        if (challenge is null || !ValidateChallenge(challenge))
        {
            throw new ArgumentException($"Unable to parse HTTP bearer from '{challenge}'.", challenge);
        }

        var matches = BearerRegex.Matches(challenge);

        string? realm = null;
        string? service = null;
        string? scope = null;

        foreach (Match match in matches)
        {
            realm ??= GetGroupValue(match, RealmParameter);
            service ??= GetGroupValue(match, ServiceParameter);
            scope ??= GetGroupValue(match, ScopeParameter);
        }

        if (realm is null)
        {
            throw new ArgumentException($"Unable to parse realm from '{challenge}'.", challenge);
        }

        if (service is null)
        {
            throw new ArgumentException($"Unable to parse service from '{challenge}'.", challenge);
        }

        if (scope is null)
        {
            throw new ArgumentException($"Unable to parse scope from '{challenge}'.", challenge);
        }

        return new HttpBearerChallenge(realm, service, scope);
    }

    private static string? GetGroupValue(Match match, string groupName)
    {
        Group group = match.Groups[groupName];
        return group.Success ? group.Value : null;
    }

    private static bool ValidateChallenge(string? challenge)
    {
        if (String.IsNullOrEmpty(challenge) || !BearerRegex.IsMatch(challenge))
        {
            return false;
        }

        return true;
    }
}
