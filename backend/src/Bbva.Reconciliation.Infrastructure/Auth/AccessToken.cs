namespace Bbva.Reconciliation.Infrastructure.Auth;

/// <summary>An OAuth 2.0 access token together with the instant it stops being usable.</summary>
/// <param name="Value">The bearer token.</param>
/// <param name="ExpiresAt">Absolute expiry, already adjusted by the configured renewal margin.</param>
public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt)
{
    public bool IsUsableAt(DateTimeOffset instant) => instant < ExpiresAt;
}

/// <summary>Obtains bearer tokens for BBVA API Market.</summary>
public interface IBbvaTokenProvider
{
    /// <summary>
    /// Returns a valid token, reusing the cached one when it has not expired.
    /// Concurrent callers share a single refresh rather than stampeding the token endpoint.
    /// </summary>
    Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default);

    /// <summary>Discards the cached token so the next call fetches a fresh one.</summary>
    void Invalidate();
}
