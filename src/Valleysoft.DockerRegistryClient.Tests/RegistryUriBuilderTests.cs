using System.Globalization;
using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RegistryUriBuilderTests
{
    private static readonly Uri Origin = new("https://registry.example:5443/");
    private static readonly string Digest = $"sha256:{new string('a', 64)}";

    [Fact]
    public void Endpoints_PreserveNestedRepositoryAndPathBoundaries()
    {
        const string repository = "team/project/image";
        Assert.Equal("/v2/_catalog", RegistryUriBuilder.Catalog(Origin, null).AbsolutePath);
        Assert.Equal($"/v2/{repository}/tags/list", RegistryUriBuilder.Tags(Origin, repository, null).AbsolutePath);
        Assert.Equal($"/v2/{repository}/blobs/{Digest}", RegistryUriBuilder.Blob(Origin, repository, Digest).AbsolutePath);
        Assert.Equal($"/v2/{repository}/blobs/uploads/", RegistryUriBuilder.Upload(Origin, repository).AbsolutePath);
        Assert.Equal($"/v2/{repository}/manifests/v1.2-ABC", RegistryUriBuilder.Manifest(Origin, repository, "v1.2-ABC").AbsolutePath);
        Assert.Equal($"/v2/{repository}/manifests/{Digest}", RegistryUriBuilder.Manifest(Origin, repository, Digest).AbsolutePath);
        Uri referrers = RegistryUriBuilder.Referrers(Origin, repository, Digest);
        Assert.Equal($"/v2/{repository}/referrers/{Digest}", referrers.AbsolutePath);
        Assert.Equal(Origin.Authority, referrers.Authority);
        Assert.Equal(string.Empty, referrers.Fragment);
        Assert.Equal(string.Empty, referrers.Query);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData(0, "?n=0")]
    [InlineData(-1, "?n=-1")]
    [InlineData(50, "?n=50")]
    public void Count_PreservesSemanticsAndUsesInvariantFormatting(int? count, string expected)
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            var culture = (CultureInfo)CultureInfo.InvariantCulture.Clone();
            culture.NumberFormat.NegativeSign = "~";
            CultureInfo.CurrentCulture = culture;
            Assert.Equal(expected, RegistryUriBuilder.Catalog(Origin, count).Query);
            Assert.Equal(expected, RegistryUriBuilder.Tags(Origin, "repo", count).Query);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [Fact]
    public void MountAndArtifactType_EncodeRawValuesOnce()
    {
        Uri mount = RegistryUriBuilder.Mount(Origin, "team/dest", Digest, "team/source");
        Assert.Equal($"?mount=sha256%3A{new string('a', 64)}&from=team%2Fsource", mount.Query);
        const string filter = "application/example+json;name=a&b=#?%20 \u00e9";
        Uri referrers = RegistryUriBuilder.Referrers(Origin, "repo", Digest, filter);
        Assert.Equal($"?artifactType={Uri.EscapeDataString(filter)}", referrers.Query);
        Assert.Equal(filter, Uri.UnescapeDataString(referrers.Query.Substring("?artifactType=".Length)));
        Assert.Equal(string.Empty, referrers.Fragment);
    }

    [Theory]
    [InlineData("/upload", "", "")]
    [InlineData("/upload?", "", "")]
    [InlineData("/upload#section", "", "#section")]
    [InlineData("/upload?#section", "", "#section")]
    [InlineData("/upload?state=a%2Fb%2Bc%3D#section", "state=a%2Fb%2Bc%3D&", "#section")]
    [InlineData("/upload?state=a+b&empty=&flag#section", "state=a+b&empty=&flag&", "#section")]
    [InlineData("/upload??opaque#section", "?opaque&", "#section")]
    public void QueryAddition_PreservesServerStateAndFragment(string location, string prefix, string fragment)
    {
        Uri result = RegistryUriBuilder.AddQueryParameter(new Uri(Origin, location), "digest", Digest);
        Assert.Equal($"/upload", result.AbsolutePath);
        Assert.Equal($"?{prefix}digest={Uri.EscapeDataString(Digest)}", result.Query);
        Assert.Equal(fragment, result.Fragment);
        Assert.Equal(Origin.Authority, result.Authority);
    }

    [Theory]
    [InlineData("registry.example", "https", "registry.example", 443)]
    [InlineData("registry.example/", "https", "registry.example", 443)]
    [InlineData("http://localhost:5000/", "http", "localhost", 5000)]
    [InlineData("https://bücher.example:5443/", "https", "xn--bcher-kva.example", 5443)]
    [InlineData("xn--bcher-kva.example", "https", "xn--bcher-kva.example", 443)]
    [InlineData("https://[::1]:5000", "https", "::1", 5000)]
    public void RegistryOrigin_ValidHosts_RetainAuthority(string registry, string scheme, string idnHost, int port)
    {
        using var client = new RegistryClient(registry);
        Assert.Equal(scheme, client.BaseUri.Scheme);
        Assert.Equal(idnHost, client.BaseUri.IdnHost);
        Assert.Equal(port, client.BaseUri.Port);
        Assert.Equal("/", client.BaseUri.AbsolutePath);
        Assert.Equal("", client.BaseUri.Query);
        Assert.Equal("", client.BaseUri.Fragment);
        Assert.Equal(client.BaseUri.Authority, client.Registry);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("https://")]
    [InlineData("https://registry.example/path")]
    [InlineData("registry.example/path")]
    [InlineData("https://registry.example//")]
    [InlineData("https://registry.example/.")]
    [InlineData("https://registry.example/..")]
    [InlineData("https://registry.example/path/..")]
    [InlineData("https://registry.example/%2e")]
    [InlineData("https://registry.example?")]
    [InlineData("https://registry.example/?")]
    [InlineData("https://registry.example?query=value")]
    [InlineData("https://registry.example#")]
    [InlineData("https://registry.example/#fragment")]
    [InlineData("https://registry.example\\path")]
    [InlineData("https://registry.example\n")]
    [InlineData("ftp://registry.example")]
    [InlineData("https://user:password@registry.example")]
    public void RegistryOrigin_InvalidAddress_IsAnArgumentError(string registry)
    {
        Assert.Equal("registry", Assert.Throws<ArgumentException>(() => new RegistryClient(registry)).ParamName);
    }

    [Theory]
    [InlineData("https://registry.example", "https://registry.example:443/next", true)]
    [InlineData("https://registry.example", "https://other.example/next", false)]
    [InlineData("https://registry.example", "https://registry.example:444/next", false)]
    [InlineData("https://registry.example", "http://registry.example/next", false)]
    [InlineData("https://bücher.example", "https://xn--bcher-kva.example:443/next", true)]
    public void SameOrigin_ComparesSchemeCanonicalHostAndEffectivePort(string origin, string destination, bool expected)
    {
        Assert.Equal(expected, RegistryUriBuilder.HasSameOrigin(new Uri(origin), new Uri(destination)));
    }
}
