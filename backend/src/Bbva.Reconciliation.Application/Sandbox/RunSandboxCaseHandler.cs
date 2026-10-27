using System.Diagnostics;
using System.Text.Json;
using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Domain.Errors;

namespace Bbva.Reconciliation.Application.Sandbox;

/// <summary>Outcome of executing one sandbox scenario against the live sandbox.</summary>
/// <param name="CaseId">The scenario identifier.</param>
/// <param name="Description">The scenario description, echoed for reporting.</param>
/// <param name="Outcome">Whether the scenario matched, drifted, or could not run.</param>
/// <param name="ExpectedStatusCode">Status code the workbook documents.</param>
/// <param name="ActualStatusCode">Status code the sandbox returned.</param>
/// <param name="ExpectedResponse">The documented response body.</param>
/// <param name="ActualResponse">The body the sandbox returned.</param>
/// <param name="Differences">Located structural differences, empty when the bodies match.</param>
/// <param name="ElapsedMilliseconds">Wall-clock duration of the call.</param>
public sealed record SandboxCaseResult(
    string CaseId,
    string Description,
    SandboxOutcome Outcome,
    int ExpectedStatusCode,
    int ActualStatusCode,
    JsonElement? ExpectedResponse,
    JsonElement? ActualResponse,
    IReadOnlyList<JsonDifference> Differences,
    long ElapsedMilliseconds)
{
    public bool StatusMatches => ExpectedStatusCode == ActualStatusCode;

    public bool BodyMatches => Differences.Count == 0;
}

/// <summary>How a sandbox scenario turned out.</summary>
public enum SandboxOutcome
{
    /// <summary>Status code and response body both match the documentation.</summary>
    Passed,

    /// <summary>The status code matches but the body drifted — usually moving sandbox data.</summary>
    Drifted,

    /// <summary>The status code differs from what the documentation promises.</summary>
    Failed,

    /// <summary>The call could not be made at all.</summary>
    Errored,
}

/// <summary>
/// Executes sandbox scenarios against the real sandbox and diffs the outcome against the
/// documented expectation.
/// </summary>
/// <remarks>
/// Amounts and balances are excluded from the body comparison by default. The workbook
/// captured them at a point in time and the sandbox data set keeps moving, so comparing
/// them would paint every scenario red and hide the differences that actually matter:
/// status codes, error messages and response shape.
/// </remarks>
public sealed class RunSandboxCaseHandler(
    ISandboxTestPlanRepository repository,
    ISandboxRequestExecutor executor)
{
    /// <summary>Paths whose values legitimately change between sandbox runs.</summary>
    public static readonly IReadOnlySet<string> VolatilePaths = new HashSet<string>(StringComparer.Ordinal)
    {
        "movements.postedBalance.amount",
        "movements.transaction.amount",
        "movements.valuationDate",
        "movements.operation.date",
        "postedBalance.amount",
        "transaction.amount",
        "valuationDate",
        "operation.date",
        "pagination.totalElements",
        "pagination.totalPages",
        "pagination.keys",
    };

    private readonly ISandboxTestPlanRepository _repository =
        repository ?? throw new ArgumentNullException(nameof(repository));

    private readonly ISandboxRequestExecutor _executor =
        executor ?? throw new ArgumentNullException(nameof(executor));

    public async Task<Result<SandboxCaseResult>> HandleAsync(
        string caseId,
        bool compareVolatileValues = false,
        CancellationToken cancellationToken = default)
    {
        var testCase = _repository.GetPlan().Find(caseId);

        if (testCase is null)
        {
            return Result<SandboxCaseResult>.Failure(
                DomainError.Bad($"Unknown sandbox case '{caseId}'."));
        }

        var stopwatch = Stopwatch.StartNew();

        var execution = await _executor
            .ExecuteAsync(testCase.Method, testCase.ResolvedPath(), testCase.RequestBody, cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        var ignored = compareVolatileValues ? null : VolatilePaths;
        var differences = JsonComparer.Compare(testCase.ExpectedResponse, execution.Body, ignored);

        var outcome = execution.StatusCode switch
        {
            0 => SandboxOutcome.Errored,
            var status when status != testCase.ExpectedStatusCode => SandboxOutcome.Failed,
            _ when differences.Count > 0 => SandboxOutcome.Drifted,
            _ => SandboxOutcome.Passed,
        };

        return Result<SandboxCaseResult>.Success(new SandboxCaseResult(
            testCase.Id,
            testCase.Description,
            outcome,
            testCase.ExpectedStatusCode,
            execution.StatusCode,
            testCase.ExpectedResponse,
            execution.Body,
            differences,
            stopwatch.ElapsedMilliseconds));
    }
}
