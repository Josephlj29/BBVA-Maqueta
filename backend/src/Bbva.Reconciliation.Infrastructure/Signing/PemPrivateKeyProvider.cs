using System.Security.Cryptography;
using System.Text;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.Signing;

/// <summary>
/// Loads the BBVA signing key once, from a PEM file or a base64 encoded PEM string.
/// </summary>
/// <remarks>
/// <para>
/// Both PKCS#8 (<c>BEGIN PRIVATE KEY</c>) and PKCS#1 (<c>BEGIN RSA PRIVATE KEY</c>) are
/// accepted: the BBVA credential bundle ships one of each, and which one a given
/// environment received is not predictable. <see cref="RSA.ImportFromPem"/> dispatches on
/// the header, so no manual armor stripping is needed.
/// </para>
/// <para>
/// The key is loaded eagerly in the constructor. A malformed key is a deployment fault,
/// and surfacing it at startup beats discovering it as an unexplained 401 in production.
/// </para>
/// </remarks>
public sealed class PemPrivateKeyProvider : IPrivateKeyProvider, IDisposable
{
    private readonly RSA _rsa;

    public PemPrivateKeyProvider(IOptions<BbvaOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var settings = options.Value;
        var pem = ReadPem(settings);

        _rsa = RSA.Create();

        try
        {
            _rsa.ImportFromPem(pem);
        }
        catch (ArgumentException exception)
        {
            _rsa.Dispose();
            throw new InvalidOperationException(
                "The configured BBVA private key is not a valid PEM encoded RSA key. Expected a " +
                "PKCS#8 ('BEGIN PRIVATE KEY') or PKCS#1 ('BEGIN RSA PRIVATE KEY') block.",
                exception);
        }
        catch (CryptographicException exception)
        {
            _rsa.Dispose();
            throw new InvalidOperationException(
                "The configured BBVA private key could not be imported. If it is password " +
                "protected, decrypt it first — encrypted keys are not supported.",
                exception);
        }
    }

    public RSA GetPrivateKey() => _rsa;

    public void Dispose() => _rsa.Dispose();

    private static string ReadPem(BbvaOptions settings)
    {
        if (!string.IsNullOrWhiteSpace(settings.PrivateKeyBase64))
        {
            return DecodeBase64Pem(settings.PrivateKeyBase64);
        }

        if (string.IsNullOrWhiteSpace(settings.PrivateKeyPath))
        {
            throw new InvalidOperationException(
                $"{BbvaOptions.SectionName}: neither PrivateKeyPath nor PrivateKeyBase64 is configured.");
        }

        var path = Path.GetFullPath(settings.PrivateKeyPath);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"BBVA private key not found at '{path}'. Set {BbvaOptions.SectionName}:PrivateKeyPath " +
                "to the PEM file, or supply the key inline via PrivateKeyBase64.",
                path);
        }

        return File.ReadAllText(path);
    }

    /// <summary>
    /// Decodes the base64-of-PEM form used by the reference Bruno environment. That value
    /// is sometimes stored with the PEM armor already stripped, so the armor is restored
    /// and the body re-wrapped at 64 characters when it is missing.
    /// </summary>
    private static string DecodeBase64Pem(string encoded)
    {
        byte[] decoded;

        try
        {
            decoded = Convert.FromBase64String(encoded.Trim());
        }
        catch (FormatException exception)
        {
            throw new InvalidOperationException(
                $"{BbvaOptions.SectionName}:PrivateKeyBase64 is not valid base64.",
                exception);
        }

        var pem = Encoding.UTF8.GetString(decoded).Trim();

        if (pem.Contains("-----BEGIN", StringComparison.Ordinal))
        {
            return pem;
        }

        var body = pem.Replace("\r", string.Empty, StringComparison.Ordinal)
                      .Replace("\n", string.Empty, StringComparison.Ordinal);

        var wrapped = string.Join('\n', Chunk(body, 64));

        return $"-----BEGIN PRIVATE KEY-----\n{wrapped}\n-----END PRIVATE KEY-----";
    }

    private static IEnumerable<string> Chunk(string value, int size)
    {
        for (var offset = 0; offset < value.Length; offset += size)
        {
            yield return value.Substring(offset, Math.Min(size, value.Length - offset));
        }
    }
}
