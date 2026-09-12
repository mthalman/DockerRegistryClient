using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class RegistryReferenceValidatorTests
{
    [Theory]
    [InlineData("repo")]
    [InlineData("team/project/image")]
    [InlineData("a.b/c_d/e__f/g---h/123")]
    public void Repository_ValidNames_AreAccepted(string value) =>
        RegistryReferenceValidator.ValidateRepository(value, "repositoryName");

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Repo")]
    [InlineData("répo")]
    [InlineData("/repo")]
    [InlineData("repo/")]
    [InlineData("team//repo")]
    [InlineData("team/../repo")]
    [InlineData("team/./repo")]
    [InlineData("team\\repo")]
    [InlineData("repo?x=1")]
    [InlineData("repo#fragment")]
    [InlineData("repo&x=1")]
    [InlineData("repo%2Fimage")]
    [InlineData("repo%252Fimage")]
    [InlineData("a..b")]
    [InlineData("a___b")]
    [InlineData("-repo")]
    [InlineData("repo_")]
    [InlineData("repo\n")]
    [InlineData("repo\0")]
    public void Repository_InvalidNames_IdentifyArgument(string value)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(
            () => RegistryReferenceValidator.ValidateRepository(value, "sourceRepositoryName"));
        Assert.Equal("sourceRepositoryName", exception.ParamName);
    }

    [Fact]
    public void Repository_LengthBoundary_IsEnforced()
    {
        RegistryReferenceValidator.ValidateRepository(new string('a', 255), "repositoryName");
        Assert.Throws<ArgumentException>(() =>
            RegistryReferenceValidator.ValidateRepository(new string('a', 256), "repositoryName"));
    }

    [Theory]
    [InlineData("_release")]
    [InlineData("v1.2-ABC_3")]
    [InlineData("123")]
    public void Tag_ValidValues_AreAccepted(string value) =>
        RegistryReferenceValidator.ValidateTag(value, "tag");

    [Theory]
    [InlineData("")]
    [InlineData(".tag")]
    [InlineData("-tag")]
    [InlineData("tag/sub")]
    [InlineData("tag\\sub")]
    [InlineData("tag?query")]
    [InlineData("tag#fragment")]
    [InlineData("tag&query")]
    [InlineData("tag%23fragment")]
    [InlineData("täg")]
    [InlineData("tag\n")]
    [InlineData("tag ")]
    public void Tag_InvalidValues_AreRejected(string value) =>
        Assert.Throws<ArgumentException>(() => RegistryReferenceValidator.ValidateTag(value, "tag"));

    [Fact]
    public void Tag_LengthBoundary_IsEnforced()
    {
        RegistryReferenceValidator.ValidateTag(new string('a', 128), "tag");
        Assert.Throws<ArgumentException>(() =>
            RegistryReferenceValidator.ValidateTag(new string('a', 129), "tag"));
    }

    [Theory]
    [InlineData("sha256", 64)]
    [InlineData("sha384", 96)]
    [InlineData("sha512", 128)]
    [InlineData("blake3", 64)]
    public void Digest_KnownAlgorithms_RequireExactLowerHex(string algorithm, int length)
    {
        string valid = $"{algorithm}:{new string('a', length)}";
        RegistryReferenceValidator.ValidateDigest(valid, "digest");
        Assert.True(RegistryReferenceValidator.IsValidReference(valid));
        foreach (string encoded in new[]
        {
            new string('a', length - 1), new string('a', length + 1),
            new string('A', length), new string('g', length), "abc"
        })
        {
            Assert.Throws<ArgumentException>(() =>
                RegistryReferenceValidator.ValidateDigest($"{algorithm}:{encoded}", "digest"));
        }
    }

    [Theory]
    [InlineData("1algo:AbC_=-")]
    [InlineData("custom.sha+variant-v1_2:aZ09_=-")]
    public void Digest_GenericAlgorithms_RetainSupportedGrammar(string value) =>
        RegistryReferenceValidator.ValidateDigest(value, "digest");

    [Theory]
    [InlineData("")]
    [InlineData("latest")]
    [InlineData("sha256:abc")]
    [InlineData("SHA256:abc")]
    [InlineData("algo:")]
    [InlineData(":abc")]
    [InlineData("algo::abc")]
    [InlineData("algo:abc?x=1")]
    [InlineData("algo:abc#x")]
    [InlineData("algo:abc&x=1")]
    [InlineData("algo:abc/path")]
    [InlineData("algo:abc\\path")]
    [InlineData("algo:abc%3F")]
    [InlineData("algo:abc\n")]
    [InlineData("algo:abc+def")]
    [InlineData("algo:é")]
    public void Digest_InvalidValues_AreRejected(string value) =>
        Assert.Throws<ArgumentException>(() => RegistryReferenceValidator.ValidateDigest(value, "digest"));

    [Fact]
    public void RequiredReferences_Null_IdentifyOriginalParameter()
    {
        Action<string, string>[] validators =
        [
            RegistryReferenceValidator.ValidateRepository,
            RegistryReferenceValidator.ValidateTag,
            RegistryReferenceValidator.ValidateDigest,
            RegistryReferenceValidator.ValidateReference
        ];
        foreach (Action<string, string> validate in validators)
        {
            Assert.Equal("input", Assert.Throws<ArgumentNullException>(() => validate(null!, "input")).ParamName);
        }
    }
}
