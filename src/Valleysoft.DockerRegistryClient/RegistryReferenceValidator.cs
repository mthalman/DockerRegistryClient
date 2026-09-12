using System.Text.RegularExpressions;

namespace Valleysoft.DockerRegistryClient;

internal static class RegistryReferenceValidator
{
    private static readonly Regex RepositoryRegex = new(
        @"\A[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*(?:/[a-z0-9]+(?:(?:[._]|__|-+)[a-z0-9]+)*)*\z",
        RegexOptions.CultureInvariant);
    private static readonly Regex TagRegex = new(
        @"\A[A-Za-z0-9_][A-Za-z0-9._-]{0,127}\z",
        RegexOptions.CultureInvariant);
    private static readonly Regex DigestRegex = new(
        @"\A[a-z0-9]+(?:[+._-][a-z0-9]+)*:[A-Za-z0-9=_-]+\z",
        RegexOptions.CultureInvariant);

    public static void ValidateRepository(string value, string parameterName) =>
        Validate(value, parameterName,
            value is not null && value.Length <= 255 && RepositoryRegex.IsMatch(value),
            "The value must be a valid repository name of at most 255 characters.");

    public static void ValidateTag(string value, string parameterName) =>
        Validate(value, parameterName, IsValidTag(value),
            "The value must be a valid manifest tag of at most 128 characters.");

    public static void ValidateDigest(string value, string parameterName) =>
        Validate(value, parameterName, IsValidDigest(value),
            "The value must be a valid digest with an algorithm and encoded value.");

    public static void ValidateReference(string value, string parameterName) =>
        Validate(value, parameterName, IsValidReference(value),
            "The value must be a valid manifest tag or digest.");

    public static bool IsValidReference(string? value) =>
        IsValidTag(value) || IsValidDigest(value);

    private static bool IsValidTag(string? value) =>
        value is not null && TagRegex.IsMatch(value);

    public static bool IsValidDigest(string? value)
    {
        if (value is null || !DigestRegex.IsMatch(value))
        {
            return false;
        }

        int separatorIndex = value.IndexOf(':');
        string algorithm = value.Substring(0, separatorIndex);
        string encoded = value.Substring(separatorIndex + 1);
        return algorithm switch
        {
            "sha256" => IsLowerHex(encoded, 64),
            "sha384" => IsLowerHex(encoded, 96),
            "sha512" => IsLowerHex(encoded, 128),
            "blake3" => IsLowerHex(encoded, 64),
            _ => true
        };
    }

    private static bool IsLowerHex(string value, int expectedLength) =>
        value.Length == expectedLength &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static void Validate(string? value, string parameterName, bool isValid, string message)
    {
        if (value is null)
        {
            throw new ArgumentNullException(parameterName);
        }

        if (!isValid)
        {
            throw new ArgumentException(message, parameterName);
        }
    }
}
