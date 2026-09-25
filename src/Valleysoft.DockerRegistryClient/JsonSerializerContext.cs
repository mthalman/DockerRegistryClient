using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using DockerManifestReference = Valleysoft.DockerRegistryClient.Models.Manifests.Docker.ManifestReference;
using OciManifestReference = Valleysoft.DockerRegistryClient.Models.Manifests.Oci.ManifestReference;
using Valleysoft.DockerRegistryClient.Models;
using Valleysoft.DockerRegistryClient.Models.Images;
using Valleysoft.DockerRegistryClient.Models.Manifests;
using Valleysoft.DockerRegistryClient.Models.Manifests.Docker;
using Valleysoft.DockerRegistryClient.Models.Manifests.Oci;

namespace Valleysoft.DockerRegistryClient;

[JsonSourceGenerationOptions(
    GenerationMode = JsonSourceGenerationMode.Metadata,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Catalog))]
[JsonSerializable(typeof(RepositoryTags))]
[JsonSerializable(typeof(ErrorResult))]
[JsonSerializable(typeof(Error))]
[JsonSerializable(typeof(Image))]
[JsonSerializable(typeof(ImageConfig))]
[JsonSerializable(typeof(RootFilesystem))]
[JsonSerializable(typeof(LayerHistory))]
[JsonSerializable(typeof(ManifestList), TypeInfoPropertyName = "DockerManifestList")]
[JsonSerializable(typeof(DockerManifest), TypeInfoPropertyName = "DockerManifest")]
[JsonSerializable(typeof(ManifestConfig))]
[JsonSerializable(typeof(ManifestLayer))]
[JsonSerializable(typeof(DockerManifestReference), TypeInfoPropertyName = "DockerManifestReference")]
[JsonSerializable(typeof(DockerManifestReference[]), TypeInfoPropertyName = "DockerManifestReferenceArray")]
[JsonSerializable(typeof(OciImageManifest), TypeInfoPropertyName = "OciImageManifest")]
[JsonSerializable(typeof(OciImageIndex), TypeInfoPropertyName = "OciImageIndex")]
[JsonSerializable(typeof(OciDescriptor))]
[JsonSerializable(typeof(ManifestPlatform))]
[JsonSerializable(typeof(OciManifestReference), TypeInfoPropertyName = "OciManifestReference")]
[JsonSerializable(typeof(OciManifestReference[]), TypeInfoPropertyName = "OciManifestReferenceArray")]
[JsonSerializable(typeof(OAuthToken))]
internal partial class DockerRegistryClientJsonContext : JsonSerializerContext
{
}

internal static class DockerRegistryClientJson
{
    internal static T Deserialize<T>(string content)
        where T : class
    {
        T? result = JsonSerializer.Deserialize(content, GetTypeInfo<T>());
        return result ?? throw new JsonException($"Unable to deserialize content:{Environment.NewLine}{content}");
    }

    internal static T? DeserializeNullable<T>(string content)
        where T : class =>
        JsonSerializer.Deserialize(content, GetTypeInfo<T>());

    internal static byte[] SerializeManifest(IManifest manifest)
    {
        if (manifest is RawManifest rawManifest)
        {
            return rawManifest.Content.ToArray();
        }

        Type manifestType = manifest.GetType();
        if (manifestType == typeof(DockerManifest))
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                (DockerManifest)manifest,
                DockerRegistryClientJsonContext.Default.DockerManifest);
        }

        if (manifestType == typeof(ManifestList))
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                (ManifestList)manifest,
                DockerRegistryClientJsonContext.Default.DockerManifestList);
        }

        if (manifestType == typeof(OciImageManifest))
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                (OciImageManifest)manifest,
                DockerRegistryClientJsonContext.Default.OciImageManifest);
        }

        if (manifestType == typeof(OciImageIndex))
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                (OciImageIndex)manifest,
                DockerRegistryClientJsonContext.Default.OciImageIndex);
        }

        throw new NotSupportedException(
            $"Serializing the manifest type '{manifestType.FullName}' is not supported. " +
            "Publish the manifest with the overload that accepts a JsonTypeInfo<TManifest>, " +
            "or publish its content as a RawManifest.");
    }

    private static JsonTypeInfo<T> GetTypeInfo<T>()
        where T : class =>
        (JsonTypeInfo<T>)GetTypeInfo(typeof(T));

    private static JsonTypeInfo GetTypeInfo(Type type) => type switch
    {
        Type t when t == typeof(Catalog) => DockerRegistryClientJsonContext.Default.Catalog,
        Type t when t == typeof(RepositoryTags) => DockerRegistryClientJsonContext.Default.RepositoryTags,
        Type t when t == typeof(ErrorResult) => DockerRegistryClientJsonContext.Default.ErrorResult,
        Type t when t == typeof(Image) => DockerRegistryClientJsonContext.Default.Image,
        Type t when t == typeof(OAuthToken) => DockerRegistryClientJsonContext.Default.OAuthToken,
        Type t when t == typeof(DockerManifest) => DockerRegistryClientJsonContext.Default.DockerManifest,
        Type t when t == typeof(ManifestList) => DockerRegistryClientJsonContext.Default.DockerManifestList,
        Type t when t == typeof(OciImageManifest) => DockerRegistryClientJsonContext.Default.OciImageManifest,
        Type t when t == typeof(OciImageIndex) => DockerRegistryClientJsonContext.Default.OciImageIndex,
        _ => throw new NotSupportedException($"JSON metadata is not available for type '{type.FullName}'.")
    };
}
