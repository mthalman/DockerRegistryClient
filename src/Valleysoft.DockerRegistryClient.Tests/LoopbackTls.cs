using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Valleysoft.DockerRegistryClient.Tests;

internal static class LoopbackTls
{
    public static X509Certificate2 CreateCertificate()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=localhost",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        var alternativeNames = new SubjectAlternativeNameBuilder();
        alternativeNames.AddDnsName("localhost");
        alternativeNames.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(alternativeNames.Build());
        request.CertificateExtensions.Add(
            new X509BasicConstraintsExtension(false, false, 0, false));
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(
                X509KeyUsageFlags.DigitalSignature |
                X509KeyUsageFlags.KeyEncipherment,
                false));

        using X509Certificate2 ephemeralCertificate = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-1),
            DateTimeOffset.UtcNow.AddHours(1));
        const string password = "loopback-test";
        byte[] certificateBytes = ephemeralCertificate.Export(
            X509ContentType.Pkcs12,
            password);
        return X509CertificateLoader.LoadPkcs12(
            certificateBytes,
            password,
            X509KeyStorageFlags.MachineKeySet |
            X509KeyStorageFlags.Exportable);
    }

    public static async Task<SslStream> AuthenticateServerAsync(
        Stream stream,
        X509Certificate2 certificate,
        CancellationToken cancellationToken)
    {
        var sslStream = new SslStream(stream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsServerAsync(
            new SslServerAuthenticationOptions
            {
                ServerCertificate = certificate,
                EnabledSslProtocols =
                    SslProtocols.Tls12 | SslProtocols.Tls13
            },
            cancellationToken);
        return sslStream;
    }
}
