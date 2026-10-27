using System.ComponentModel.DataAnnotations;

namespace Bbva.Reconciliation.Infrastructure.Configuration;

/// <summary>
/// Everything needed to talk to BBVA API Market. Bound from the <c>Bbva</c> configuration
/// section and validated at startup, so a misconfigured deployment fails immediately
/// instead of on the first customer request.
/// </summary>
public sealed class BbvaOptions
{
    public const string SectionName = "Bbva";

    /// <summary>
    /// API Market root, without a trailing slash.
    /// Sandbox: <c>https://sbx.pe.bbvaapimarket.com</c>.
    /// Live: <c>https://apis.pe.bbvaapimarket.com</c>.
    /// </summary>
    [Required]
    [Url]
    public string BaseUrl { get; set; } = "https://sbx.pe.bbvaapimarket.com";

    /// <summary>OAuth 2.0 client identifier issued by BBVA.</summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>OAuth 2.0 client secret. Supply via user-secrets or environment, never appsettings.</summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Path to the RSA private key PEM registered with BBVA. Either this or
    /// <see cref="PrivateKeyBase64"/> must be supplied.
    /// </summary>
    public string? PrivateKeyPath { get; set; }

    /// <summary>
    /// The private key PEM, base64 encoded. Matches the <c>base64</c> variable used by the
    /// reference Bruno collection, so an existing environment can be reused as-is.
    /// </summary>
    public string? PrivateKeyBase64 { get; set; }

    /// <summary>
    /// Whether to add <c>expires</c> to the <c>Signature</c> header. The captured working
    /// signature omits it; enable only if BBVA asks for it.
    /// </summary>
    public bool IncludeSignatureExpires { get; set; }

    /// <summary>
    /// Sends <c>x-virtualized: true</c> on the token request, which makes the sandbox
    /// return canned responses instead of hitting the simulated core.
    /// </summary>
    public bool Virtualized { get; set; }

    /// <summary>How long before actual expiry a cached access token is considered stale.</summary>
    [Range(typeof(TimeSpan), "00:00:05", "00:10:00")]
    public TimeSpan TokenRenewalMargin { get; set; } = TimeSpan.FromSeconds(60);

    /// <summary>Per-request timeout for calls to BBVA.</summary>
    [Range(typeof(TimeSpan), "00:00:01", "00:05:00")]
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>Host component of <see cref="BaseUrl"/>, used for the signed <c>host</c> line.</summary>
    public string Host => new Uri(BaseUrl, UriKind.Absolute).Host;

    /// <summary>
    /// Validates the parts data annotations cannot express: exactly one private key source.
    /// </summary>
    public IEnumerable<string> ValidateKeySource()
    {
        var hasPath = !string.IsNullOrWhiteSpace(PrivateKeyPath);
        var hasInline = !string.IsNullOrWhiteSpace(PrivateKeyBase64);

        if (!hasPath && !hasInline)
        {
            yield return
                $"{SectionName}: supply either PrivateKeyPath or PrivateKeyBase64 — request signing is mandatory.";
        }

        if (hasPath && hasInline)
        {
            yield return
                $"{SectionName}: PrivateKeyPath and PrivateKeyBase64 are mutually exclusive; configure exactly one.";
        }
    }
}
