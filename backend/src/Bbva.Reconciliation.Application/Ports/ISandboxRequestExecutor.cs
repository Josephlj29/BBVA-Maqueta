using System.Text.Json;

namespace Bbva.Reconciliation.Application.Ports;

/// <summary>The raw outcome of a sandbox call, before it is judged against expectations.</summary>
/// <param name="StatusCode">HTTP status code, or 0 when the call could not be completed.</param>
/// <param name="Body">Parsed response body, or <c>null</c> when there was none or it was unparseable.</param>
/// <param name="RawBody">The body exactly as received, for display.</param>
/// <param name="FailureReason">Why the call could not be made, when <paramref name="StatusCode"/> is 0.</param>
public sealed record SandboxExecution(
    int StatusCode,
    JsonElement? Body,
    string RawBody,
    string? FailureReason = null);

/// <summary>
/// Sends an arbitrary signed request to BBVA and returns the untouched response.
/// </summary>
/// <remarks>
/// This deliberately bypasses the domain model. The sandbox test plan includes bodies that
/// are invalid by design — 19-digit accounts, a <c>moneyFlow</c> of <c>ENTRY</c>, dates in
/// the year 2030 — and their whole purpose is to observe what the bank replies. Routing
/// them through local validation would reject them before they ever leave, and the plan
/// could not be certified.
/// </remarks>
public interface ISandboxRequestExecutor
{
    Task<SandboxExecution> ExecuteAsync(
        string method,
        string path,
        JsonElement? body,
        CancellationToken cancellationToken = default);
}
