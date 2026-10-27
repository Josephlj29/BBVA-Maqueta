using System.Text;
using Bbva.Reconciliation.Infrastructure.Signing;
using Microsoft.Extensions.Time.Testing;

namespace Bbva.Reconciliation.Infrastructure.Tests.Signing;

/// <summary>
/// The signing algorithm is the single point where this integration succeeds or fails,
/// and BBVA gives no diagnostics beyond a blank 401. These tests pin it against a real
/// captured exchange plus a locally generated key pair.
/// </summary>
public sealed class RsaSha512RequestSignerTests
{
    private static readonly DateTimeOffset CapturedDate =
        new(2026, 8, 16, 3, 35, 57, TimeSpan.Zero);

    /// <summary>
    /// Digest captured from the working Bruno environment (<c>Credenciales_API_PAY</c>)
    /// for the body <c>{"contracts":["001109100100500997"]}</c>. If this ever stops
    /// matching, the request body serialization drifted and every call will 401.
    /// </summary>
    private const string CapturedDigest =
        "SHA-512=GSAgyW9sj/HVINRImvMlhJGht0tUciPszaKeSHQJCW2jkHS0PWrewCylBBxwcr7zvcw0RbjGL66btG0rXWTlBQ==";

    private const string CapturedBody = """{"contracts":["001109100100500997"]}""";

    private static RsaSha512RequestSigner CreateSigner(
        TestKey key,
        bool includeExpires = false,
        DateTimeOffset? now = null)
    {
        var clock = new FakeTimeProvider(now ?? CapturedDate);
        return new RsaSha512RequestSigner(key.Provider, clock, includeExpires);
    }

    [Fact]
    public void Digest_matches_the_captured_production_value()
    {
        var digest = RsaSha512RequestSigner.ComputeDigest(CapturedBody);

        digest.ShouldBe(CapturedDigest);
    }

    [Fact]
    public void Digest_of_an_empty_body_is_the_sha512_of_the_empty_string()
    {
        // GET requests carry no body but still send a Digest header. Both the WinForms
        // reference client and the Bruno script hash the empty string here.
        var digest = RsaSha512RequestSigner.ComputeDigest(string.Empty);

        digest.ShouldBe(
            "SHA-512=z4PhNX7vuL3xVChQ1m2AB9Yg5AULVxXcg/SpIdNs6c5H0NE8XYXysP+DGNKHfuwvY7kxvUdBeoGlODJ6+SfaPg==");
    }

    [Fact]
    public void String_to_sign_has_exactly_four_lf_separated_lines()
    {
        var stringToSign = RsaSha512RequestSigner.BuildStringToSign(
            HttpMethod.Post,
            "/pe/business-reconciliation/v1/transactions/search",
            "sbx.pe.bbvaapimarket.com",
            "Sun, 16 Aug 2026 03:35:57 GMT",
            CapturedDigest);

        stringToSign.ShouldBe(
            "(request-target):post /pe/business-reconciliation/v1/transactions/search\n" +
            "host:sbx.pe.bbvaapimarket.com\n" +
            "date:Sun, 16 Aug 2026 03:35:57 GMT\n" +
            $"digest:{CapturedDigest}");

        stringToSign.ShouldNotContain("\r", Case.Sensitive);
        stringToSign.Split('\n').Length.ShouldBe(4);
    }

    [Fact]
    public void String_to_sign_lowercases_the_method()
    {
        var stringToSign = RsaSha512RequestSigner.BuildStringToSign(
            HttpMethod.Get, "/x", "h", "d", "g");

        stringToSign.ShouldStartWith("(request-target):get /x\n");
    }

    [Fact]
    public void Date_uses_rfc1123_in_utc()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var signed = signer.Sign(HttpMethod.Get, "/x", "sbx.pe.bbvaapimarket.com", null);

        signed.Date.ShouldBe("Sun, 16 Aug 2026 03:35:57 GMT");
        signed.Date.ShouldEndWith("GMT");
    }

    [Fact]
    public void Signature_verifies_against_the_public_key()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var signed = signer.Sign(
            HttpMethod.Post,
            "/pe/business-reconciliation/v1/transactions/search",
            "sbx.pe.bbvaapimarket.com",
            CapturedBody);

        key.Verify(signed.StringToSign, ExtractSignature(signed.SignatureHeader)).ShouldBeTrue();
    }

    [Fact]
    public void Signature_header_declares_the_algorithm_and_signed_headers()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var signed = signer.Sign(HttpMethod.Get, "/x", "h", null);

        signed.SignatureHeader.ShouldStartWith(
            """algorithm="rsa-sha512",headers="(request-target) host date digest",signature=""");
        signed.SignatureHeader.ShouldEndWith("\"");
    }

    [Fact]
    public void Expires_is_omitted_by_default()
    {
        // The captured working signature carries no expires component; the WinForms
        // reference client adds one. Default to the shape that is known to be accepted.
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        signer.Sign(HttpMethod.Get, "/x", "h", null).SignatureHeader.ShouldNotContain("expires=");
    }

    [Fact]
    public void Expires_is_one_hour_ahead_when_enabled()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key, includeExpires: true);

        var signed = signer.Sign(HttpMethod.Get, "/x", "h", null);

        var expectedExpiry = CapturedDate.ToUnixTimeSeconds() + 3600;
        signed.SignatureHeader.ShouldContain($"expires=\"{expectedExpiry}\"");
    }

    [Fact]
    public void Signing_the_same_request_twice_at_the_same_instant_is_deterministic()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var first = signer.Sign(HttpMethod.Post, "/x", "h", CapturedBody);
        var second = signer.Sign(HttpMethod.Post, "/x", "h", CapturedBody);

        // PKCS#1 v1.5 is deterministic, unlike PSS. This guards against an accidental
        // switch to a randomised padding scheme, which BBVA would reject.
        second.SignatureHeader.ShouldBe(first.SignatureHeader);
    }

    [Fact]
    public void A_different_path_produces_a_different_signature()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var search = signer.Sign(HttpMethod.Post, "/a", "h", CapturedBody);
        var detail = signer.Sign(HttpMethod.Post, "/b", "h", CapturedBody);

        detail.SignatureHeader.ShouldNotBe(search.SignatureHeader);
    }

    [Fact]
    public void Query_string_is_excluded_from_the_signed_path()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var signed = signer.Sign(HttpMethod.Get, "/accounts/1?pageSize=10", "h", null);

        signed.StringToSign.ShouldStartWith("(request-target):get /accounts/1\n");
    }

    [Fact]
    public void Headers_expose_every_value_bbva_requires()
    {
        using var key = TestKey.Create();
        var signer = CreateSigner(key);

        var signed = signer.Sign(HttpMethod.Post, "/x", "sbx.pe.bbvaapimarket.com", CapturedBody);

        signed.Digest.ShouldBe(CapturedDigest);
        signed.Host.ShouldBe("sbx.pe.bbvaapimarket.com");
        signed.Date.ShouldNotBeNullOrWhiteSpace();
        signed.SignatureHeader.ShouldNotBeNullOrWhiteSpace();
    }

    private static byte[] ExtractSignature(string header)
    {
        const string marker = "signature=\"";
        var start = header.IndexOf(marker, StringComparison.Ordinal) + marker.Length;
        var end = header.IndexOf('"', start);
        return Convert.FromBase64String(header[start..end]);
    }

    /// <summary>A throwaway RSA key pair, so tests never touch the real BBVA credential.</summary>
    private sealed class TestKey : IDisposable
    {
        private readonly System.Security.Cryptography.RSA _rsa;

        private TestKey(System.Security.Cryptography.RSA rsa)
        {
            _rsa = rsa;
            Provider = new InMemoryPrivateKeyProvider(rsa);
        }

        public IPrivateKeyProvider Provider { get; }

        public static TestKey Create() =>
            new(System.Security.Cryptography.RSA.Create(2048));

        public bool Verify(string stringToSign, byte[] signature) =>
            _rsa.VerifyData(
                Encoding.UTF8.GetBytes(stringToSign),
                signature,
                System.Security.Cryptography.HashAlgorithmName.SHA512,
                System.Security.Cryptography.RSASignaturePadding.Pkcs1);

        public void Dispose() => _rsa.Dispose();
    }

    private sealed class InMemoryPrivateKeyProvider(System.Security.Cryptography.RSA rsa) : IPrivateKeyProvider
    {
        public System.Security.Cryptography.RSA GetPrivateKey() => rsa;
    }
}
