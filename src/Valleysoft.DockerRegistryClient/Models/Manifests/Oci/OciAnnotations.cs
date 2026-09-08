namespace Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

/// <summary>
/// Pre-defined annotation keys from the OCI spec.
/// </summary>
/// <remarks>
/// See the <see href="https://github.com/opencontainers/image-spec/blob/v1.0/annotations.md">OCI Image Format Specification annotations</see>.
/// </remarks>
public static class OciAnnotations
{
    /// <summary>
    /// Namespace prefix reserved by the Open Container Initiative.
    /// </summary>
    public const string Prefix = "org.opencontainers";

    /// <summary>
    /// Namespace prefix for predefined OCI image annotations.
    /// </summary>
    public const string ImagePrefix = $"{Prefix}.image";

    /// <summary>
    /// Annotation key for the RFC 3339 creation date and time.
    /// </summary>
    public const string ImageCreated = $"{ImagePrefix}.created";

    /// <summary>
    /// Annotation key for the artifact's authors.
    /// </summary>
    public const string ImageAuthors = $"{ImagePrefix}.authors";

    /// <summary>
    /// Annotation key for a URL with information about the artifact.
    /// </summary>
    public const string ImageUrl = $"{ImagePrefix}.url";

    /// <summary>
    /// Annotation key for the artifact's documentation URL.
    /// </summary>
    public const string ImageDocumentation = $"{ImagePrefix}.documentation";

    /// <summary>
    /// Annotation key for the artifact's source-code URL.
    /// </summary>
    public const string ImageSource = $"{ImagePrefix}.source";

    /// <summary>
    /// Annotation key for the artifact's version.
    /// </summary>
    public const string ImageVersion = $"{ImagePrefix}.version";

    /// <summary>
    /// Annotation key for the source-control revision.
    /// </summary>
    public const string ImageRevision = $"{ImagePrefix}.revision";

    /// <summary>
    /// Annotation key for the artifact's distributing entity.
    /// </summary>
    public const string ImageVendor = $"{ImagePrefix}.vendor";

    /// <summary>
    /// Annotation key for the SPDX license expression under which the contained software is distributed.
    /// </summary>
    public const string ImageLicenses = $"{ImagePrefix}.licenses";

    /// <summary>
    /// Annotation key for the name of the referenced image.
    /// </summary>
    public const string ImageRefName = $"{ImagePrefix}.ref.name";

    /// <summary>
    /// Annotation key for the artifact's human-readable title.
    /// </summary>
    public const string ImageTitle = $"{ImagePrefix}.title";

    /// <summary>
    /// Annotation key for the artifact's human-readable description.
    /// </summary>
    public const string ImageDescription = $"{ImagePrefix}.description";
}
