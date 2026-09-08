namespace Valleysoft.DockerRegistryClient;

internal class HttpBearerChallenge
{
    private const string ServiceParameter = "service";
    private const string ScopeParameter = "scope";
    private const string RealmParameter = "realm";
    public const string Bearer = "Bearer";

    public string Realm { get; }
    public string? Service { get; }
    public string? Scope { get; }

    public HttpBearerChallenge(string realm, string? service, string? scope)
    {
        Realm = realm;
        Service = service;
        Scope = scope;
    }

    public static HttpBearerChallenge Parse(string? challenge)
    {
        if (string.IsNullOrEmpty(challenge))
        {
            throw CreateParseException(challenge);
        }

        var parser = new HttpHeaderValueParser(challenge!);
        string? realm = null;
        string? service = null;
        string? scope = null;
        var recognizedParameters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (true)
        {
            parser.SkipOptionalWhitespace();
            while (parser.TryConsume(','))
            {
                parser.SkipOptionalWhitespace();
            }

            if (parser.End)
            {
                break;
            }

            if (!parser.TryReadToken(out string parameterName))
            {
                throw CreateParseException(challenge);
            }

            parser.SkipOptionalWhitespace();
            if (!parser.TryConsume('='))
            {
                throw CreateParseException(challenge);
            }

            parser.SkipOptionalWhitespace();
            if (!parser.TryReadTokenOrQuotedString(out string parameterValue))
            {
                throw CreateParseException(challenge);
            }

            if (IsRecognizedParameter(parameterName) &&
                !recognizedParameters.Add(parameterName))
            {
                throw CreateParseException(challenge);
            }

            if (parameterName.Equals(RealmParameter, StringComparison.OrdinalIgnoreCase))
            {
                realm = parameterValue;
            }
            else if (parameterName.Equals(ServiceParameter, StringComparison.OrdinalIgnoreCase))
            {
                service = parameterValue;
            }
            else if (parameterName.Equals(ScopeParameter, StringComparison.OrdinalIgnoreCase))
            {
                scope = parameterValue;
            }

            parser.SkipOptionalWhitespace();
            if (parser.End)
            {
                break;
            }

            if (!parser.TryConsume(','))
            {
                throw CreateParseException(challenge);
            }
        }

        if (realm is null)
        {
            throw new ArgumentException($"Unable to parse realm from '{challenge}'.", nameof(challenge));
        }

        if (!HttpHeaderValueParser.IsValidUri(realm, UriKind.Absolute))
        {
            throw new ArgumentException($"Bearer realm is not a valid absolute URI: '{realm}'.", nameof(challenge));
        }

        return new HttpBearerChallenge(realm, service, scope);
    }

    private static bool IsRecognizedParameter(string parameterName) =>
        parameterName.Equals(RealmParameter, StringComparison.OrdinalIgnoreCase) ||
        parameterName.Equals(ServiceParameter, StringComparison.OrdinalIgnoreCase) ||
        parameterName.Equals(ScopeParameter, StringComparison.OrdinalIgnoreCase);

    private static ArgumentException CreateParseException(string? challenge) =>
        new($"Unable to parse HTTP bearer from '{challenge}'.", nameof(challenge));
}
