namespace Bbva.Reconciliation.Application.Ports;

/// <summary>The complete result of signing a request, without sending it.</summary>
/// <param name="Method">HTTP method, as signed.</param>
/// <param name="Path">Path that was signed, query string already stripped.</param>
/// <param name="Host">Host line of the signed string.</param>
/// <param name="Body">Body whose digest was computed, empty for bodyless requests.</param>
/// <param name="Digest">Value of the <c>Digest</c> header.</param>
/// <param name="Date">Value of the <c>Date</c> header.</param>
/// <param name="StringToSign">The exact LF-separated string that was signed.</param>
/// <param name="Signature">Value of the <c>Signature</c> header.</param>
/// <param name="Headers">Every header that would be sent, except <c>Authorization</c>.</param>
public sealed record SignaturePreview(
    string Method,
    string Path,
    string Host,
    string Body,
    string Digest,
    string Date,
    string StringToSign,
    string Signature,
    IReadOnlyDictionary<string, string> Headers);

/// <summary>
/// Computes a request signature for inspection.
/// </summary>
/// <remarks>
/// This exists because BBVA answers a bad signature with a bare 401 and no diagnostics.
/// Being able to see the exact string that was signed — where the line breaks fall,
/// whether the method is lowercased, whether the query string was stripped — turns an
/// afternoon of guessing into a two-minute comparison. It replaces the WinForms tool that
/// shipped with the documentation.
/// </remarks>
public interface ISignaturePreviewService
{
    /// <param name="method">HTTP method.</param>
    /// <param name="path">Absolute path, with or without a query string.</param>
    /// <param name="body">Request body, or <c>null</c> for a bodyless request.</param>
    SignaturePreview Preview(string method, string path, string? body);
}
