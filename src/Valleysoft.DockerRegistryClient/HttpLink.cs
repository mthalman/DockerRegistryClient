namespace Valleysoft.DockerRegistryClient;

internal class HttpLink
{
    private const string AnchorParameter = "anchor";
    private const string RelationshipParameter = "rel";
    private readonly string[] relationships;

    public HttpLink(string url, string relationship, string? anchor = null)
    {
        Url = url;
        Relationship = relationship;
        Anchor = anchor;
        relationships = relationship.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries);
    }

    public string Url { get; }
    public string Relationship { get; }
    public string? Anchor { get; }

    public bool HasRelationship(string relationship) =>
        relationships.Contains(relationship, StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string value, out HttpLink? httpLink)
    {
        httpLink = null;
        if (!TryParseList(value, out IReadOnlyList<HttpLink>? links) || links!.Count != 1)
        {
            return false;
        }

        httpLink = links[0];
        return true;
    }

    public static bool TryParseList(string? value, out IReadOnlyList<HttpLink>? links)
    {
        links = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        var parser = new HttpHeaderValueParser(value!);
        var parsedLinks = new List<HttpLink>();

        while (true)
        {
            parser.SkipOptionalWhitespace();
            while (parser.TryConsume(','))
            {
                parser.SkipOptionalWhitespace();
            }

            if (parser.End)
            {
                if (parsedLinks.Count == 0)
                {
                    return false;
                }

                links = parsedLinks;
                return true;
            }

            if (!TryParseLink(parser, out HttpLink? link))
            {
                return false;
            }

            parsedLinks.Add(link!);
            parser.SkipOptionalWhitespace();
            if (parser.End)
            {
                links = parsedLinks;
                return true;
            }

            if (!parser.TryConsume(','))
            {
                return false;
            }
        }
    }

    private static bool TryParseLink(HttpHeaderValueParser parser, out HttpLink? link)
    {
        link = null;
        if (!parser.TryConsume('<') ||
            !parser.TryReadLinkTarget(out string url) ||
            !parser.TryConsume('>') ||
            !HttpHeaderValueParser.IsValidUri(url, UriKind.RelativeOrAbsolute))
        {
            return false;
        }

        string? anchor = null;
        string? relationship = null;

        while (true)
        {
            parser.SkipOptionalWhitespace();
            if (!parser.TryConsume(';'))
            {
                break;
            }

            parser.SkipOptionalWhitespace();
            if (!parser.TryReadToken(out string parameterName))
            {
                return false;
            }

            parser.SkipOptionalWhitespace();
            string? parameterValue = null;
            if (parser.TryConsume('='))
            {
                parser.SkipOptionalWhitespace();
                if (!parser.TryReadTokenOrQuotedString(out parameterValue))
                {
                    return false;
                }
            }

            if (parameterName.Equals(AnchorParameter, StringComparison.OrdinalIgnoreCase))
            {
                if (anchor is null)
                {
                    if (parameterValue is null ||
                        !HttpHeaderValueParser.IsValidUri(
                            parameterValue,
                            UriKind.RelativeOrAbsolute,
                            allowEmpty: true))
                    {
                        return false;
                    }

                    anchor = parameterValue;
                }

                continue;
            }

            if (!parameterName.Equals(RelationshipParameter, StringComparison.OrdinalIgnoreCase) ||
                relationship is not null)
            {
                // RFC 8288 requires recipients to ignore rel occurrences after the first.
                continue;
            }

            if (parameterValue is null || !TryValidateRelationships(parameterValue))
            {
                return false;
            }

            relationship = parameterValue;
        }

        if (relationship is null)
        {
            return false;
        }

        link = new HttpLink(url, relationship!, anchor);
        return true;
    }

    private static bool TryValidateRelationships(string value)
    {
        string[] relationships = value.Split(
            [' ', '\t'],
            StringSplitOptions.RemoveEmptyEntries);
        return relationships.Length > 0 && relationships.All(IsValidRelationship);
    }

    private static bool IsValidRelationship(string relationship)
    {
        if (IsAsciiLetter(relationship[0]) &&
            relationship.Skip(1).All(character =>
                IsAsciiLetter(character) ||
                character is >= '0' and <= '9' ||
                character is '.' or '-'))
        {
            return true;
        }

        return HttpHeaderValueParser.IsValidUri(relationship, UriKind.Absolute);
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'a' and <= 'z' || value is >= 'A' and <= 'Z';
}
