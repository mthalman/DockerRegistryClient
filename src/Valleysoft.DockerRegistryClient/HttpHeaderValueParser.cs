using System.Text;

namespace Valleysoft.DockerRegistryClient;

internal sealed class HttpHeaderValueParser
{
    private const string UriCharacters = "-._~:/?#[]@!$&'()*+,;=";
    private readonly string value;
    private int position;

    public HttpHeaderValueParser(string value)
    {
        this.value = value;
    }

    public bool End => position == value.Length;

    public void SkipOptionalWhitespace()
    {
        while (!End && (value[position] == ' ' || value[position] == '\t'))
        {
            position++;
        }
    }

    public bool TryConsume(char expected)
    {
        if (End || value[position] != expected)
        {
            return false;
        }

        position++;
        return true;
    }

    public bool TryReadToken(out string token)
    {
        int start = position;
        while (!End && IsTokenCharacter(value[position]))
        {
            position++;
        }

        token = value.Substring(start, position - start);
        return token.Length > 0;
    }

    public bool TryReadTokenOrQuotedString(out string parsedValue)
    {
        if (!End && value[position] == '"')
        {
            return TryReadQuotedString(out parsedValue);
        }

        return TryReadToken(out parsedValue);
    }

    public bool TryReadLinkTarget(out string target)
    {
        int start = position;
        while (!End && value[position] != '>')
        {
            char character = value[position];
            if (char.IsControl(character) ||
                char.IsWhiteSpace(character) ||
                character is '<' or '"')
            {
                target = string.Empty;
                return false;
            }

            position++;
        }

        target = value.Substring(start, position - start);
        return target.Length > 0 && !End;
    }

    public static bool IsValidUri(
        string value,
        UriKind uriKind,
        bool allowEmpty = false)
    {
        if (value.Length == 0)
        {
            return allowEmpty;
        }

        // Rooted paths become absolute file URIs on Unix, so require the RFC scheme explicitly.
        if (uriKind == UriKind.Absolute && !HasUriScheme(value))
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (character == '%')
            {
                if (index + 2 >= value.Length ||
                    !IsHexDigit(value[index + 1]) ||
                    !IsHexDigit(value[index + 2]))
                {
                    return false;
                }

                index += 2;
            }
            else if (!IsUriCharacter(character))
            {
                return false;
            }
        }

        return Uri.TryCreate(value, uriKind, out _);
    }

    private static bool HasUriScheme(string value)
    {
        if (!IsAsciiLetter(value[0]))
        {
            return false;
        }

        for (int index = 1; index < value.Length; index++)
        {
            char character = value[index];
            if (character == ':')
            {
                return true;
            }

            if (!IsAsciiLetter(character) &&
                character is not (>= '0' and <= '9') &&
                character is not '+' and not '-' and not '.')
            {
                return false;
            }
        }

        return false;
    }

    private bool TryReadQuotedString(out string parsedValue)
    {
        position++;
        var builder = new StringBuilder();

        while (!End)
        {
            char character = value[position++];
            if (character == '"')
            {
                parsedValue = builder.ToString();
                return true;
            }

            if (character == '\\')
            {
                if (End || !IsQuotedPairCharacter(value[position]))
                {
                    parsedValue = string.Empty;
                    return false;
                }

                builder.Append(value[position++]);
                continue;
            }

            if (!IsQuotedTextCharacter(character))
            {
                parsedValue = string.Empty;
                return false;
            }

            builder.Append(character);
        }

        parsedValue = string.Empty;
        return false;
    }

    private static bool IsTokenCharacter(char value) =>
        value is >= 'a' and <= 'z' ||
        value is >= 'A' and <= 'Z' ||
        value is >= '0' and <= '9' ||
        value is '!' or '#' or '$' or '%' or '&' or '\'' or '*' or '+' or '-' or '.' or
            '^' or '_' or '`' or '|' or '~';

    private static bool IsUriCharacter(char value) =>
        value is >= 'a' and <= 'z' ||
        value is >= 'A' and <= 'Z' ||
        value is >= '0' and <= '9' ||
        UriCharacters.IndexOf(value) >= 0;

    private static bool IsHexDigit(char value) =>
        value is >= '0' and <= '9' ||
        value is >= 'a' and <= 'f' ||
        value is >= 'A' and <= 'F';

    private static bool IsAsciiLetter(char value) =>
        value is >= 'a' and <= 'z' || value is >= 'A' and <= 'Z';

    private static bool IsQuotedTextCharacter(char value) =>
        value == '\t' ||
        value == ' ' ||
        value == '!' ||
        value is >= '#' and <= '[' ||
        value is >= ']' and <= '~' ||
        value >= '\u0080';

    private static bool IsQuotedPairCharacter(char value) =>
        value == '\t' ||
        value == ' ' ||
        value is >= '!' and <= '~' ||
        value >= '\u0080';
}
