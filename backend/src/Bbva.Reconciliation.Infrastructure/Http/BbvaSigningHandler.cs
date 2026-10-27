using System.Net;
using System.Net.Http.Headers;
using Bbva.Reconciliation.Infrastructure.Auth;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Bbva.Reconciliation.Infrastructure.Signing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.Http;

/// <summary>
/// Attaches the bearer token and the BBVA two-legged message signature to every outbound
/// request.
/// </summary>
/// <remarks>
/// <para>
/// Signing belongs here rather than at each call site for one concrete reason: the
/// signature covers the final request path. The WinForms reference client computed the
/// signature first and appended path parameters afterwards, so any request carrying a path
/// parameter — which is every movement-detail call — was signed for the wrong target. A
/// delegating handler sees the request only once it is fully built, so that class of bug
/// cannot recur.
/// </para>
/// <para>
/// The token endpoint is skipped: it authenticates with the client secret and must not be
/// signed.
/// </para>
/// </remarks>
public sealed class BbvaSigningHandler : DelegatingHandler
{
    private readonly IBbvaTokenProvider _tokenProvider;
    private readonly RsaSha512RequestSigner _signer;
    private readonly BbvaOptions _options;
    private readonly ILogger<BbvaSigningHandler> _logger;

    public BbvaSigningHandler(
        IBbvaTokenProvider tokenProvider,
        RsaSha512RequestSigner signer,
        IOptions<BbvaOptions> options,
        ILogger<BbvaSigningHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(tokenProvider);
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _tokenProvider = tokenProvider;
        _signer = signer;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var uri = request.RequestUri
            ?? throw new InvalidOperationException("Cannot sign a request without a URI.");

        if (uri.AbsolutePath.EndsWith(BbvaOAuthTokenProvider.TokenPath, StringComparison.Ordinal))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        await ApplyCredentialsAsync(request, uri, cancellationToken).ConfigureAwait(false);

        var response = await base.SendAsync(request, cancellationToken).ConfigureAwait(false);

        // A 401 usually means the cached token expired earlier than advertised. Retry once
        // with a fresh token; a second 401 is a real credential or signature problem.
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            _logger.RetryingAfterUnauthorized(uri.AbsolutePath);
            response.Dispose();

            _tokenProvider.Invalidate();

            using var retry = await CloneAsync(request, cancellationToken).ConfigureAwait(false);
            await ApplyCredentialsAsync(retry, uri, cancellationToken).ConfigureAwait(false);

            return await base.SendAsync(retry, cancellationToken).ConfigureAwait(false);
        }

        return response;
    }

    private async Task ApplyCredentialsAsync(
        HttpRequestMessage request,
        Uri uri,
        CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var signed = _signer.Sign(request.Method, uri.PathAndQuery, _options.Host, body);
        var token = await _tokenProvider.GetTokenAsync(cancellationToken).ConfigureAwait(false);

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Value);

        // TryAddWithoutValidation is required: Digest and Signature are not known to
        // HttpClient's header parser, and Host is otherwise reserved.
        Replace(request, "Digest", signed.Digest);
        Replace(request, "Signature", signed.SignatureHeader);
        Replace(request, "Date", signed.Date);
        Replace(request, "Host", signed.Host);

        _logger.RequestSigned(request.Method.Method, uri.AbsolutePath);
    }

    private static void Replace(HttpRequestMessage request, string name, string value)
    {
        request.Headers.Remove(name);
        request.Headers.TryAddWithoutValidation(name, value);
    }

    /// <summary>
    /// Rebuilds a request for the retry. An <see cref="HttpRequestMessage"/> cannot be sent
    /// twice, and its content stream has already been consumed by the signing step.
    /// </summary>
    private static async Task<HttpRequestMessage> CloneAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri)
        {
            Version = request.Version,
            VersionPolicy = request.VersionPolicy,
        };

        if (request.Content is not null)
        {
            var body = await request.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
            var content = new ByteArrayContent(body);

            foreach (var header in request.Content.Headers)
            {
                content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            clone.Content = content;
        }

        foreach (var option in request.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        return clone;
    }
}

internal static partial class SigningHandlerLog
{
    [LoggerMessage(
        EventId = 1101,
        Level = LogLevel.Debug,
        Message = "Signed {Method} {Path} for BBVA")]
    public static partial void RequestSigned(this ILogger logger, string method, string path);

    [LoggerMessage(
        EventId = 1102,
        Level = LogLevel.Warning,
        Message = "BBVA returned 401 for {Path}; refreshing the token and retrying once")]
    public static partial void RetryingAfterUnauthorized(this ILogger logger, string path);
}
