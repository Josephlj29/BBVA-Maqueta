using Bbva.Reconciliation.Api.Contracts;
using Bbva.Reconciliation.Api.Errors;
using Bbva.Reconciliation.Application.Movements;

namespace Bbva.Reconciliation.Api.Endpoints;

/// <summary>The two business endpoints the BBVA API exposes, wrapped for this application.</summary>
public static class MovementEndpoints
{
    public static IEndpointRouteBuilder MapMovementEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/v1")
            .WithTags("Movements");

        group.MapPost("/transactions/search", SearchAsync)
            .WithName("SearchMovements")
            .WithSummary("List account movements")
            .WithDescription(
                "Lists movements for a single 18-digit contract. An account with no matching " +
                "movements returns 200 with an infoMessage of \"No transactions to show.\" — " +
                "that is a successful empty result, not an error. Page through results by " +
                "passing the previous response's pagination.next back as paginationKey.")
            .Produces<MovementPageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        group.MapGet("/accounts/{accountId}/transactions/{transactionId}", GetAsync)
            .WithName("GetMovement")
            .WithSummary("Get one movement")
            .WithDescription(
                "Retrieves a single movement. accountId must be 18 digits; transactionId must " +
                "be exactly 31 alphanumeric characters, e.g. PE00110201300210484377000000353.")
            .Produces<MovementResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        return routes;
    }

    private static async Task<IResult> SearchAsync(
        SearchMovementsApiRequest request,
        SearchMovementsHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(
            new SearchMovementsRequest(
                request.Contracts,
                request.Currency,
                request.FromDate,
                request.ToDate,
                request.MoneyFlow,
                request.Keyword,
                request.PageSize,
                request.PaginationKey),
            cancellationToken);

        return result.Match(
            page => Results.Ok(MovementPageResponse.From(page)),
            error => error.ToResult());
    }

    private static async Task<IResult> GetAsync(
        string accountId,
        string transactionId,
        GetMovementHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(accountId, transactionId, cancellationToken);

        return result.Match(
            movement => Results.Ok(MovementResponse.From(movement)),
            error => error.ToResult());
    }
}
