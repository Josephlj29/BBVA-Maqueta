using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Bbva.Reconciliation.Infrastructure.Signing;

/// <summary>
/// Implements the BBVA API Connect two-legged (B2B) message signature.
/// </summary>
/// <remarks>
/// <para>The algorithm, in order:</para>
/// <list type="number">
///   <item><description><c>digest = "SHA-512=" + base64(SHA512(bodyUtf8))</c>, hashing the empty string for bodyless requests.</description></item>
///   <item><description>Build a four line, LF separated string covering <c>(request-target)</c>, <c>host</c>, <c>date</c> and <c>digest</c>.</description></item>
///   <item><description>Sign it with RSA PKCS#1 v1.5 over SHA-512 and base64 the result.</description></item>
/// </list>
/// <para>
/// Three details are load bearing and easy to get wrong: the separator is LF and never
/// CRLF; the HTTP method is lowercased; and the signed path excludes the query string.
/// A mismatch in any of them yields a bare 401 with no explanation from the bank.
/// </para>
/// <para>
/// This mirrors the two reference implementations shipped with the BBVA documentation
/// (the WinForms sample and the Bruno pre-request script), which agree on everything
/// except the optional <c>expires</c> component — see <c>includeExpires</c>.
/// </para>
/// </remarks>
public sealed class RsaSha512RequestSigner
{
    private const string DigestPrefix = "SHA-512=";
    private const string Algorithm = "rsa-sha512";
    private const string SignedHeaders = "(request-target) host date digest";

    /// <summary>Validity window applied when <c>expires</c> is enabled.</summary>
    private static readonly TimeSpan ExpiryWindow = TimeSpan.FromHours(1);

    private readonly IPrivateKeyProvider _keyProvider;
    private readonly TimeProvider _clock;
    private readonly bool _includeExpires;

    /// <param name="keyProvider">Supplies the RSA private key registered with BBVA.</param>
    /// <param name="clock">Source of the <c>Date</c> header; injected so tests are deterministic.</param>
    /// <param name="includeExpires">
    /// Whether to add <c>expires="{unixSeconds}"</c> to the signature header. The captured
    /// working signature omits it, so this defaults to <c>false</c>; the WinForms sample
    /// includes it, hence the switch.
    /// </param>
    public RsaSha512RequestSigner(
        IPrivateKeyProvider keyProvider,
        TimeProvider clock,
        bool includeExpires = false)
    {
        ArgumentNullException.ThrowIfNull(keyProvider);
        ArgumentNullException.ThrowIfNull(clock);

        _keyProvider = keyProvider;
        _clock = clock;
        _includeExpires = includeExpires;
    }

    /// <summary>Signs one request and returns every header value it produced.</summary>
    /// <param name="method">HTTP method; lowercased into the signed string.</param>
    /// <param name="pathAndQuery">
    /// Absolute path, optionally with a query string. The query is stripped before signing,
    /// matching the reference Bruno script.
    /// </param>
    /// <param name="host">Host header value, e.g. <c>sbx.pe.bbvaapimarket.com</c>.</param>
    /// <param name="body">Serialized request body, or <c>null</c> for bodyless requests.</param>
    public SignedRequest Sign(HttpMethod method, string pathAndQuery, string host, string? body)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(pathAndQuery);
        ArgumentException.ThrowIfNullOrWhiteSpace(host);

        var digest = ComputeDigest(body ?? string.Empty);
        var now = _clock.GetUtcNow();
        var date = FormatDate(now);
        var path = StripQuery(pathAndQuery);
        var stringToSign = BuildStringToSign(method, path, host, date, digest);

        var signature = Convert.ToBase64String(
            _keyProvider.GetPrivateKey().SignData(
                Encoding.UTF8.GetBytes(stringToSign),
                HashAlgorithmName.SHA512,
                RSASignaturePadding.Pkcs1));

        var header = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"algorithm=\"{Algorithm}\"")
            .Append(CultureInfo.InvariantCulture, $",headers=\"{SignedHeaders}\"");

        if (_includeExpires)
        {
            var expires = now.Add(ExpiryWindow).ToUnixTimeSeconds();
            header.Append(CultureInfo.InvariantCulture, $",expires=\"{expires}\"");
        }

        header.Append(CultureInfo.InvariantCulture, $",signature=\"{signature}\"");

        return new SignedRequest(digest, date, host, stringToSign, header.ToString());
    }

    /// <summary>
    /// Computes the <c>Digest</c> header value. A bodyless request hashes the empty
    /// string rather than omitting the header.
    /// </summary>
    public static string ComputeDigest(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        var hash = SHA512.HashData(Encoding.UTF8.GetBytes(body));
        return DigestPrefix + Convert.ToBase64String(hash);
    }

    /// <summary>
    /// Builds the canonical string. The separator is LF: using <c>Environment.NewLine</c>
    /// would emit CRLF on Windows and silently break every signature.
    /// </summary>
    public static string BuildStringToSign(
        HttpMethod method,
        string path,
        string host,
        string date,
        string digest)
    {
        ArgumentNullException.ThrowIfNull(method);

        var verb = method.Method.ToLowerInvariant();

        return $"(request-target):{verb} {path}\n" +
               $"host:{host}\n" +
               $"date:{date}\n" +
               $"digest:{digest}";
    }

    /// <summary>Formats the <c>Date</c> header as RFC 1123 in UTC, e.g. <c>Sun, 16 Aug 2026 03:35:57 GMT</c>.</summary>
    public static string FormatDate(DateTimeOffset instant) =>
        instant.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);

    private static string StripQuery(string pathAndQuery)
    {
        var separator = pathAndQuery.IndexOf('?', StringComparison.Ordinal);
        return separator < 0 ? pathAndQuery : pathAndQuery[..separator];
    }
}
