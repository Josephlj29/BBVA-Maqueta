using System.Text;
using System.Text.Json;
using Bbva.Reconciliation.Application.Ports;
using Microsoft.Extensions.Logging;

namespace Bbva.Reconciliation.Infrastructure.Sandbox;

/// <summary>
/// Sends a sandbox scenario's request verbatim and returns whatever the bank replies.
/// </summary>
/// <remarks>
/// The request travels through the same signed pipeline as production traffic, so a
/// scenario exercises the real signature — but the body is passed through untouched. That
/// is the point: most of the plan consists of deliberately malformed requests whose value
/// lies entirely in the bank's response.
/// </remarks>
public sealed class HttpSandboxRequestExecutor(
    HttpClient httpClient,
    ILogger<HttpSandboxRequestExecutor> logger) : ISandboxRequestExecutor
{
    private readonly HttpClient _httpClient =
        httpClient ?? throw new ArgumentNullException(nameof(httpClient));

    private readonly ILogger<HttpSandboxRequestExecutor> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<SandboxExecution> ExecuteAsync(
        string method,
        string path,
        JsonElement? body,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        using var request = new HttpRequestMessage(new HttpMethod(method.ToUpperInvariant()), path);

        if (body is { } payload && payload.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined)
        {
            // Compact, no indentation: the digest covers these exact bytes, so the body
            // must be serialised the same way it is signed.
            request.Content = new StringContent(
                payload.GetRawText(),
                Encoding.UTF8,
                "application/json");
        }

        try
        {
            using var response = await _httpClient
                .SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken)
                .ConfigureAwait(false);

            var raw = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            return new SandboxExecution((int)response.StatusCode, TryParse(raw), raw);
        }
        catch (HttpRequestException exception)
        {
            _logger.SandboxCallFailed(method, path, exception.Message);

            return new SandboxExecution(0, null, string.Empty, exception.Message);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.SandboxCallFailed(method, path, "the request timed out");

            return new SandboxExecution(0, null, string.Empty, exception.Message);
        }
    }

    private static JsonElement? TryParse(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(raw).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}

internal static partial class SandboxExecutorLog
{
    [LoggerMessage(
        EventId = 1301,
        Level = LogLevel.Warning,
        Message = "Sandbox call {Method} {Path} could not be completed: {Reason}")]
    public static partial void SandboxCallFailed(this ILogger logger, string method, string path, string reason);
}
