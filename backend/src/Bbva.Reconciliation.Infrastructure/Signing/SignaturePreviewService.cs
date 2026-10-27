using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.Signing;

/// <summary>
/// Signs a request with the real key and returns every intermediate value, without
/// sending anything.
/// </summary>
public sealed class SignaturePreviewService(
    RsaSha512RequestSigner signer,
    IOptions<BbvaOptions> options) : ISignaturePreviewService
{
    private readonly RsaSha512RequestSigner _signer =
        signer ?? throw new ArgumentNullException(nameof(signer));

    private readonly BbvaOptions _options =
        (options ?? throw new ArgumentNullException(nameof(options))).Value;

    public SignaturePreview Preview(string method, string path, string? body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var httpMethod = new HttpMethod(method.ToUpperInvariant());
        var normalizedBody = body ?? string.Empty;

        var signed = _signer.Sign(httpMethod, path, _options.Host, normalizedBody);

        return new SignaturePreview(
            httpMethod.Method,
            StripQuery(path),
            signed.Host,
            normalizedBody,
            signed.Digest,
            signed.Date,
            signed.StringToSign,
            signed.SignatureHeader,
            signed.ToHeaders());
    }

    private static string StripQuery(string path)
    {
        var separator = path.IndexOf('?', StringComparison.Ordinal);
        return separator < 0 ? path : path[..separator];
    }
}
