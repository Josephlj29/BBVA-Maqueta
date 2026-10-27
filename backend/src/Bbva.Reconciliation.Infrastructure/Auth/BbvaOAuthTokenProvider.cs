using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.Auth;

/// <summary>
/// Fetches and caches bearer tokens from <c>POST {baseUrl}/auth/oauth/v2/token</c> using
/// the client credentials grant.
/// </summary>
/// <remarks>
/// This step is missing entirely from the WinForms reference client, where the operator
/// pastes a token by hand. The endpoint is documented only in the Bruno collection and the
/// flow cheat-sheet, not in the API's technical PDF.
/// <para>
/// The token endpoint is deliberately excluded from message signing: it authenticates with
/// the client secret, and signing it would fail. The signing handler skips it by path.
/// </para>
/// </remarks>
public sealed class BbvaOAuthTokenProvider : IBbvaTokenProvider, IDisposable
{
    /// <summary>Path of the token endpoint, relative to the API Market root.</summary>
    public const string TokenPath = "/auth/oauth/v2/token";

    /// <summary>Fallback lifetime when the bank omits <c>expires_in</c>.</summary>
    private static readonly TimeSpan DefaultLifetime = TimeSpan.FromMinutes(10);

    private readonly HttpClient _httpClient;
    private readonly BbvaOptions _options;
    private readonly TimeProvider _clock;
    private readonly ILogger<BbvaOAuthTokenProvider> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    private AccessToken? _cached;

    public BbvaOAuthTokenProvider(
        HttpClient httpClient,
        IOptions<BbvaOptions> options,
        TimeProvider clock,
        ILogger<BbvaOAuthTokenProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _options = options.Value;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AccessToken> GetTokenAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();

        if (_cached is { } cached && cached.IsUsableAt(now))
        {
            return cached;
        }

        await _refreshLock.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // Re-check inside the lock: while we waited, another caller may have refreshed.
            now = _clock.GetUtcNow();

            if (_cached is { } refreshed && refreshed.IsUsableAt(now))
            {
                return refreshed;
            }

            _cached = await RequestTokenAsync(now, cancellationToken).ConfigureAwait(false);
            return _cached;
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public void Invalidate() => _cached = null;

    public void Dispose() => _refreshLock.Dispose();

    private async Task<AccessToken> RequestTokenAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, TokenPath)
        {
            Content = new FormUrlEncodedContent(
            [
                new KeyValuePair<string, string>("grant_type", "client_credentials"),
                new KeyValuePair<string, string>("client_id", _options.ClientId),
                new KeyValuePair<string, string>("client_secret", _options.ClientSecret),
            ]),
        };

        if (_options.Virtualized)
        {
            request.Headers.TryAddWithoutValidation("x-virtualized", "true");
        }

        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            _logger.TokenRequestFailed((int)response.StatusCode, Truncate(body));

            throw new BbvaAuthenticationException(
                $"BBVA rejected the token request with HTTP {(int)response.StatusCode}. " +
                "Verify Bbva:ClientId and Bbva:ClientSecret.");
        }

        var payload = await response.Content
            .ReadFromJsonAsync<TokenResponse>(cancellationToken)
            .ConfigureAwait(false);

        if (payload is null || string.IsNullOrWhiteSpace(payload.AccessToken))
        {
            throw new BbvaAuthenticationException(
                "BBVA returned a success status for the token request but no access_token.");
        }

        var lifetime = payload.ExpiresIn is > 0
            ? TimeSpan.FromSeconds(payload.ExpiresIn.Value)
            : DefaultLifetime;

        // Expire early so a token never dies mid-flight on the BBVA side.
        var margin = lifetime > _options.TokenRenewalMargin ? _options.TokenRenewalMargin : TimeSpan.Zero;
        var expiresAt = now.Add(lifetime).Subtract(margin);

        _logger.TokenObtained(lifetime.TotalSeconds, expiresAt);

        return new AccessToken(payload.AccessToken, expiresAt);
    }

    private static string Truncate(string value) =>
        value.Length <= 512 ? value : value[..512] + "…";

    private sealed record TokenResponse(
        [property: JsonPropertyName("access_token")] string? AccessToken,
        [property: JsonPropertyName("token_type")] string? TokenType,
        [property: JsonPropertyName("expires_in")] int? ExpiresIn);
}

/// <summary>Raised when BBVA refuses to issue an access token.</summary>
public sealed class BbvaAuthenticationException : Exception
{
    public BbvaAuthenticationException(string message)
        : base(message)
    {
    }

    public BbvaAuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public BbvaAuthenticationException()
    {
    }
}

internal static partial class TokenProviderLog
{
    [LoggerMessage(
        EventId = 1001,
        Level = LogLevel.Error,
        Message = "BBVA token request failed with status {StatusCode}. Body: {Body}")]
    public static partial void TokenRequestFailed(this ILogger logger, int statusCode, string body);

    [LoggerMessage(
        EventId = 1002,
        Level = LogLevel.Information,
        Message = "Obtained BBVA access token, lifetime {LifetimeSeconds}s, cached until {ExpiresAt:O}")]
    public static partial void TokenObtained(this ILogger logger, double lifetimeSeconds, DateTimeOffset expiresAt);
}
