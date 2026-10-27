using System.Security.Cryptography;
using System.Text;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Bbva.Reconciliation.Infrastructure.Signing;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.Tests.Signing;

/// <summary>
/// The BBVA credential bundle ships the signing key in more than one encoding, and the
/// reference Bruno environment stores it base64 encoded. All accepted forms must load.
/// </summary>
public sealed class PemPrivateKeyProviderTests : IDisposable
{
    private readonly List<string> _tempFiles = [];

    [Fact]
    public void Loads_a_pkcs8_pem_file()
    {
        using var generated = RSA.Create(2048);
        var path = WriteTempFile(generated.ExportPkcs8PrivateKeyPem());

        using var provider = CreateProvider(new BbvaOptions { PrivateKeyPath = path });

        AssertSignsAndVerifies(provider, generated);
    }

    [Fact]
    public void Loads_a_pkcs1_pem_file()
    {
        // The bundle's key.p8 is actually a PKCS#1 block despite the .p8 extension.
        using var generated = RSA.Create(2048);
        var path = WriteTempFile(generated.ExportRSAPrivateKeyPem());

        using var provider = CreateProvider(new BbvaOptions { PrivateKeyPath = path });

        AssertSignsAndVerifies(provider, generated);
    }

    [Fact]
    public void Loads_a_base64_encoded_pem()
    {
        using var generated = RSA.Create(2048);
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(generated.ExportPkcs8PrivateKeyPem()));

        using var provider = CreateProvider(new BbvaOptions { PrivateKeyBase64 = encoded });

        AssertSignsAndVerifies(provider, generated);
    }

    [Fact]
    public void Loads_a_base64_encoded_key_body_without_pem_armor()
    {
        // The Bruno environment stores the key with the armor already stripped.
        using var generated = RSA.Create(2048);
        var body = Convert.ToBase64String(generated.ExportPkcs8PrivateKey());
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(body));

        using var provider = CreateProvider(new BbvaOptions { PrivateKeyBase64 = encoded });

        AssertSignsAndVerifies(provider, generated);
    }

    [Fact]
    public void Returns_the_same_instance_on_every_call()
    {
        using var generated = RSA.Create(2048);
        var path = WriteTempFile(generated.ExportPkcs8PrivateKeyPem());

        using var provider = CreateProvider(new BbvaOptions { PrivateKeyPath = path });

        provider.GetPrivateKey().ShouldBeSameAs(provider.GetPrivateKey());
    }

    [Fact]
    public void Fails_fast_when_the_file_is_missing()
    {
        var options = new BbvaOptions { PrivateKeyPath = Path.Combine(Path.GetTempPath(), "no-such-key.pem") };

        var exception = Should.Throw<FileNotFoundException>(() => CreateProvider(options));

        exception.Message.ShouldContain("PrivateKeyPath");
    }

    [Fact]
    public void Fails_fast_when_no_key_source_is_configured()
    {
        Should.Throw<InvalidOperationException>(() => CreateProvider(new BbvaOptions()));
    }

    [Fact]
    public void Fails_fast_on_a_malformed_pem()
    {
        var path = WriteTempFile("-----BEGIN PRIVATE KEY-----\nnot a key\n-----END PRIVATE KEY-----");

        var exception = Should.Throw<InvalidOperationException>(
            () => CreateProvider(new BbvaOptions { PrivateKeyPath = path }));

        exception.Message.ShouldContain("PEM");
    }

    [Fact]
    public void Fails_fast_when_the_inline_key_is_not_base64()
    {
        var exception = Should.Throw<InvalidOperationException>(
            () => CreateProvider(new BbvaOptions { PrivateKeyBase64 = "!!! not base64 !!!" }));

        exception.Message.ShouldContain("valid base64");
    }

    [Fact]
    public void Rejects_configuring_both_key_sources()
    {
        var options = new BbvaOptions { PrivateKeyPath = "a.pem", PrivateKeyBase64 = "abc" };

        options.ValidateKeySource().ShouldContain(message => message.Contains("mutually exclusive"));
    }

    [Fact]
    public void Requires_at_least_one_key_source()
    {
        new BbvaOptions().ValidateKeySource().ShouldNotBeEmpty();
    }

    private static PemPrivateKeyProvider CreateProvider(BbvaOptions options) =>
        new(Options.Create(options));

    private static void AssertSignsAndVerifies(PemPrivateKeyProvider provider, RSA expected)
    {
        var payload = Encoding.UTF8.GetBytes("(request-target):post /x");

        var signature = provider.GetPrivateKey()
            .SignData(payload, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1);

        expected.VerifyData(payload, signature, HashAlgorithmName.SHA512, RSASignaturePadding.Pkcs1)
            .ShouldBeTrue("the loaded key must be the same key that was written");
    }

    private string WriteTempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), $"bbva-key-{Guid.NewGuid():N}.pem");
        File.WriteAllText(path, content);
        _tempFiles.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempFiles)
        {
            File.Delete(path);
        }
    }
}
