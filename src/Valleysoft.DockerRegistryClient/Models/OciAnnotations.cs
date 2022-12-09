namespace Valleysoft.DockerRegistryClient.Models;

/// <summary>
/// Pre-defined annotation keys from the OCI spec.
/// </summary>
public static class OciAnnotations
{
    // https://github.com/opencontainers/image-spec/blob/v1.0/annotations.md

    public const string Prefix = "org.opencontainers";
    public const string ImagePrefix = $"{Prefix}.image";

    public const string ImageCreated = $"{ImagePrefix}.created";
    public const string ImageAuthors = $"{ImagePrefix}.authors";
    public const string ImageUrl = $"{ImagePrefix}.url";
    public const string ImageDocumentation = $"{ImagePrefix}.documentation";
    public const string ImageSource = $"{ImagePrefix}.source";
    public const string ImageVersion = $"{ImagePrefix}.version";
    public const string ImageRevision = $"{ImagePrefix}.revision";
    public const string ImageVendor = $"{ImagePrefix}.vendor";
    public const string ImageLicenses = $"{ImagePrefix}.licenses";
    public const string ImageRefName = $"{ImagePrefix}.ref.name";
    public const string ImageTitle = $"{ImagePrefix}.title";
    public const string ImageDescription = $"{ImagePrefix}.description";
}
