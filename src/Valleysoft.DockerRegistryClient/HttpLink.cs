using System.Text.RegularExpressions;

namespace Valleysoft.DockerRegistryClient;

internal class HttpLink
{
    private const string LinkUrlGroup = "LinkUrl";
    private const string RelationshipTypeGroup = "RelationshipType";
    private static readonly Regex s_linkHeaderRegex =
        new($"<(?<{LinkUrlGroup}>.+)>;\\s*rel=\"(?<{RelationshipTypeGroup}>.+)\"");

    public HttpLink(string url, string relationship)
    {
        Url = url;
        Relationship = relationship;
    }

    public string Url { get; }
    public string Relationship { get; }

    public static bool TryParse(string value, out HttpLink? httpLink)
    {
        httpLink = null;

        Match match = s_linkHeaderRegex.Match(value);
        if (match.Success)
        {
            httpLink = new HttpLink(
                match.Groups[LinkUrlGroup].Value, match.Groups[RelationshipTypeGroup].Value);
            return true;
        }

        return false;
    }
}
