using System.Security.Cryptography;

namespace Bbva.Reconciliation.Infrastructure.Signing;

/// <summary>
/// Supplies the RSA private key registered with BBVA for request signing.
/// </summary>
/// <remarks>
/// Implementations are expected to load the key once and hand back the same instance:
/// the reference WinForms client re-read and re-imported the PEM on every request, which
/// is both slow and a needless repetition of a parsing step that can fail.
/// </remarks>
public interface IPrivateKeyProvider
{
    /// <summary>
    /// Returns the signing key. The caller must not dispose it — ownership stays with
    /// the provider, which is registered as a singleton.
    /// </summary>
    RSA GetPrivateKey();
}
