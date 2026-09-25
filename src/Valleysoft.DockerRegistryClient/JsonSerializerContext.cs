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
        JsonTypeInfo? typeInfo = GetTypeInfo(typeof(T));
        if (typeInfo is null)
        {
#pragma warning disable IL2026, IL3050
            T? result = JsonSerializer.Deserialize(content, typeof(T)) as T;
#pragma warning restore IL2026, IL3050
            return result ?? throw new JsonException($"Unable to deserialize content:{Environment.NewLine}{content}");
        }

        T? typedResult = JsonSerializer.Deserialize(content, typeInfo) as T;
        return typedResult ?? throw new JsonException($"Unable to deserialize content:{Environment.NewLine}{content}");
    }

    internal static T? DeserializeNullable<T>(string content)
        where T : class
    {
        JsonTypeInfo? typeInfo = GetTypeInfo(typeof(T));
        if (typeInfo is null)
        {
#pragma warning disable IL2026, IL3050
            return JsonSerializer.Deserialize(content, typeof(T)) as T;
#pragma warning restore IL2026, IL3050
        }

        return JsonSerializer.Deserialize(content, typeInfo) as T;
    }

    internal static byte[] SerializeManifest(IManifest manifest)
    {
        if (manifest is RawManifest rawManifest)
        {
            return rawManifest.Content.ToArray();
        }

        if (manifest is DockerManifest dockerManifest)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                dockerManifest,
                DockerRegistryClientJsonContext.Default.DockerManifest);
        }

        if (manifest is ManifestList manifestList)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                manifestList,
                DockerRegistryClientJsonContext.Default.DockerManifestList);
        }

        if (manifest is OciImageManifest ociImageManifest)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                ociImageManifest,
                DockerRegistryClientJsonContext.Default.OciImageManifest);
        }

        if (manifest is OciImageIndex ociImageIndex)
        {
            return JsonSerializer.SerializeToUtf8Bytes(
                ociImageIndex,
                DockerRegistryClientJsonContext.Default.OciImageIndex);
        }

#pragma warning disable IL2026, IL3050
        return JsonSerializer.SerializeToUtf8Bytes(manifest, manifest.GetType());
#pragma warning restore IL2026, IL3050
    }

    private static JsonTypeInfo? GetTypeInfo(Type type) => type switch
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
        _ => null
    };
}
