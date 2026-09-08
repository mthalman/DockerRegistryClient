using Xunit;

namespace Valleysoft.DockerRegistryClient.Tests;

public class HttpBearerChallengeTests
{
    [Theory]
    [InlineData(
        "realm=\"https://auth.docker.io/token\",service=\"registry.docker.io\",scope=\"repository:library/hello-world:pull\"",
        "https://auth.docker.io/token",
        "registry.docker.io",
        "repository:library/hello-world:pull")]
    [InlineData(
        "scope=\"repository:library/ubuntu:pull\", REALM = \"https://auth.example.com\" , Service=example.io",
        "https://auth.example.com",
        "example.io",
        "repository:library/ubuntu:pull")]
    [InlineData(
        "realm=\"https://auth.example.com\",scope=\"repository:team/\\\"quoted\\\":pull\",extension=\"ignored\"",
        "https://auth.example.com",
        null,
        "repository:team/\"quoted\":pull")]
    [InlineData(
        "realm=\"https://auth.example.com\"",
        "https://auth.example.com",
        null,
        null)]
    [InlineData(
        ",, realm=\"https://auth.example.com\",,service=registry.example,",
        "https://auth.example.com",
        "registry.example",
        null)]
    public void Parse_ValidChallenge_ReturnsChallenge(
        string challenge,
        string expectedRealm,
        string? expectedService,
        string? expectedScope)
    {
        HttpBearerChallenge result = HttpBearerChallenge.Parse(challenge);

        Assert.Equal(expectedRealm, result.Realm);
        Assert.Equal(expectedService, result.Service);
        Assert.Equal(expectedScope, result.Scope);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a valid challenge format")]
    [InlineData("service=\"registry.docker.io\"")]
    [InlineData("realm=\"/relative\"")]
    [InlineData("realm=\"https://auth.example/%ZZ\"")]
    [InlineData("realm=\"https://auth.example.com\",realm=\"https://other.example.com\"")]
    [InlineData("realm=\"https://auth.example.com\" service=\"registry.example\"")]
    [InlineData("realm=\"https://auth.example.com")]
    [InlineData("realm=\"https://auth.example.com\" trailing")]
    public void Parse_InvalidChallenge_ThrowsArgumentException(string? challenge)
    {
        Assert.Throws<ArgumentException>(() => HttpBearerChallenge.Parse(challenge));
    }
}
