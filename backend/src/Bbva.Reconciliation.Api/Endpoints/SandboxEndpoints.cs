using Bbva.Reconciliation.Api.Errors;
using Bbva.Reconciliation.Application.Sandbox;

namespace Bbva.Reconciliation.Api.Endpoints;

/// <summary>
/// Exposes the official BBVA sandbox test plan and runs its scenarios on demand.
/// </summary>
/// <remarks>
/// These endpoints exist to produce certification evidence: run all 41 scenarios, compare
/// each against the documented response, and export the result. They call the real
/// sandbox, so they are registered only outside production.
/// </remarks>
public static class SandboxEndpoints
{
    public static IEndpointRouteBuilder MapSandboxEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/v1/sandbox")
            .WithTags("Sandbox certification");

        group.MapGet("/cases", GetCases)
            .WithName("ListSandboxCases")
            .WithSummary("List the sandbox test plan")
            .WithDescription(
                "Returns every scenario extracted from the official BBVA sandbox test plan " +
                "workbook, including the request body and the documented expected response.")
            .Produces<SandboxTestPlan>(StatusCodes.Status200OK);

        group.MapGet("/cases/{caseId}", GetCase)
            .WithName("GetSandboxCase")
            .WithSummary("Get one sandbox scenario")
            .Produces<SandboxTestCase>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/cases/{caseId}/run", RunCaseAsync)
            .WithName("RunSandboxCase")
            .WithSummary("Run one scenario against the sandbox")
            .WithDescription(
                "Executes the scenario against the live BBVA sandbox and diffs the outcome " +
                "against the documented expectation. Amounts, balances and dates are excluded " +
                "from the comparison unless compareVolatileValues is true, because the sandbox " +
                "data set moves over time while the response shape does not.")
            .Produces<SandboxCaseResult>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static IResult GetCases(ISandboxTestPlanRepository repository) =>
        Results.Ok(repository.GetPlan());

    private static IResult GetCase(string caseId, ISandboxTestPlanRepository repository)
    {
        var testCase = repository.GetPlan().Find(caseId);

        return testCase is null
            ? Results.Problem(
                title: "Unknown sandbox case",
                detail: $"No scenario with id '{caseId}' exists in the test plan.",
                statusCode: StatusCodes.Status404NotFound)
            : Results.Ok(testCase);
    }

    private static async Task<IResult> RunCaseAsync(
        string caseId,
        bool? compareVolatileValues,
        RunSandboxCaseHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            caseId,
            compareVolatileValues ?? false,
            cancellationToken);

        return result.Match(
            outcome => Results.Ok(outcome),
            error => error.ToResult());
    }
}
