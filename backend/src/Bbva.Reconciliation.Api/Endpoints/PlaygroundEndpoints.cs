using System.Diagnostics;
using System.Text.Json;
using Bbva.Reconciliation.Application.Ports;

namespace Bbva.Reconciliation.Api.Endpoints;

/// <summary>
/// Sends an arbitrary signed request to BBVA and reports exactly what came back.
/// </summary>
/// <remarks>
/// This is the WinForms tool that shipped with the BBVA documentation, rebuilt: pick a
/// method and a path, supply a body, see the status and the response. It bypasses domain
/// validation on purpose — the whole point is to be able to send something invalid and
/// observe how the bank replies.
/// <para>
/// Registered only outside production. It is an integration aid, not a business endpoint,
/// and exposing an arbitrary-request proxy in production would be a confused-deputy hole.
/// </para>
/// </remarks>
public static class PlaygroundEndpoints
{
    /// <summary>Paths this endpoint is willing to sign and forward.</summary>
    private const string AllowedPathPrefix = "/pe/business-reconciliation/v1";

    public static IEndpointRouteBuilder MapPlaygroundEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        routes.MapPost("/api/v1/playground/execute", ExecuteAsync)
            .WithTags("Diagnostics")
            .WithName("ExecutePlaygroundRequest")
            .WithSummary("Send an arbitrary signed request to BBVA")
            .WithDescription(
                "Signs and forwards the given method, path and body to BBVA, returning the raw " +
                "status and response. Domain validation is skipped so malformed requests can be " +
                "sent deliberately to observe the bank's error handling. Restricted to paths " +
                "under /pe/business-reconciliation/v1.")
            .Produces<PlaygroundExecutionResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static async Task<IResult> ExecuteAsync(
        PlaygroundExecutionRequest request,
        ISandboxRequestExecutor executor,
        ISignaturePreviewService previewService,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Method) || string.IsNullOrWhiteSpace(request.Path))
        {
            return Problem("Both method and path are required.");
        }

        if (!request.Path.StartsWith(AllowedPathPrefix, StringComparison.Ordinal))
        {
            return Problem($"path must start with '{AllowedPathPrefix}'.");
        }

        if (!IsSupported(request.Method))
        {
            return Problem("Only GET and POST are supported by this API.");
        }

        JsonElement? body = null;

        if (!string.IsNullOrWhiteSpace(request.Body))
        {
            try
            {
                body = JsonDocument.Parse(request.Body).RootElement.Clone();
            }
            catch (JsonException exception)
            {
                return Problem($"body is not valid JSON: {exception.Message}");
            }
        }

        // Preview first so the caller sees what was signed even when the call fails.
        var signature = previewService.Preview(
            request.Method,
            request.Path,
            body?.GetRawText());

        var stopwatch = Stopwatch.StartNew();
        var execution = await executor.ExecuteAsync(request.Method, request.Path, body, cancellationToken);
        stopwatch.Stop();

        return Results.Ok(new PlaygroundExecutionResponse(
            request.Method.ToUpperInvariant(),
            request.Path,
            execution.StatusCode,
            execution.Body,
            execution.RawBody,
            execution.FailureReason,
            stopwatch.ElapsedMilliseconds,
            signature));
    }

    private static bool IsSupported(string method) =>
        string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase)
        || string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase);

    private static IResult Problem(string detail) =>
        Results.Problem(title: "Invalid parameters", detail: detail, statusCode: StatusCodes.Status400BadRequest);
}

/// <param name="Method">GET or POST.</param>
/// <param name="Path">Absolute BBVA path under /pe/business-reconciliation/v1.</param>
/// <param name="Body">Raw JSON body, or null for a bodyless request.</param>
public sealed record PlaygroundExecutionRequest(string Method, string Path, string? Body);

/// <param name="Method">The method that was sent.</param>
/// <param name="Path">The path that was sent.</param>
/// <param name="StatusCode">BBVA's status code, or 0 when the call could not be made.</param>
/// <param name="Body">The parsed response body.</param>
/// <param name="RawBody">The response exactly as received.</param>
/// <param name="FailureReason">Why the call could not be made, when applicable.</param>
/// <param name="ElapsedMilliseconds">Round-trip duration.</param>
/// <param name="Signature">Everything the signing step produced for this request.</param>
public sealed record PlaygroundExecutionResponse(
    string Method,
    string Path,
    int StatusCode,
    JsonElement? Body,
    string RawBody,
    string? FailureReason,
    long ElapsedMilliseconds,
    SignaturePreview Signature);
