namespace Bbva.Reconciliation.Infrastructure.Signing;

/// <summary>
/// Everything the signing step produces for one outbound request. Exposed as a value so
/// the API can show it in the playground without re-running the signature, and so tests
/// can assert on the exact string that was signed.
/// </summary>
/// <param name="Digest">Value of the <c>Digest</c> header, e.g. <c>SHA-512=...</c>.</param>
/// <param name="Date">Value of the <c>Date</c> header in RFC 1123 UTC.</param>
/// <param name="Host">Value of the <c>Host</c> header.</param>
/// <param name="StringToSign">The exact bytes that were signed, LF separated.</param>
/// <param name="SignatureHeader">Value of the <c>Signature</c> header.</param>
public sealed record SignedRequest(
    string Digest,
    string Date,
    string Host,
    string StringToSign,
    string SignatureHeader)
{
    /// <summary>
    /// The signed headers as a name/value map, in the order BBVA documents them.
    /// Does not include <c>Authorization</c>, which is supplied by the token provider.
    /// </summary>
    public IReadOnlyDictionary<string, string> ToHeaders() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Digest"] = Digest,
        ["Signature"] = SignatureHeader,
        ["Date"] = Date,
        ["Host"] = Host,
    };
}
