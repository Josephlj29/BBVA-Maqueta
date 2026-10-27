using Bbva.Reconciliation.Domain.Movements;
using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Api.Contracts;

/// <summary>Search request accepted by this API, mirroring the BBVA body shape.</summary>
/// <param name="Contracts">Contract numbers. BBVA accepts exactly one, of 18 digits.</param>
/// <param name="Currency">Optional ISO-4217 filter, e.g. <c>PEN</c>.</param>
/// <param name="FromDate">Optional lower bound, <c>yyyy-MM-ddTHH:mm:ss.SSSZ</c>, not before 2024-01-01.</param>
/// <param name="ToDate">Optional upper bound. Required whenever <paramref name="FromDate"/> is supplied.</param>
/// <param name="MoneyFlow">Optional <c>INCOME</c> or <c>EXPENSE</c> filter.</param>
/// <param name="Keyword">Optional free-text filter over the movement description.</param>
/// <param name="PageSize">Optional page size. Must be greater than zero.</param>
/// <param name="PaginationKey">Opaque cursor taken from a previous page's <c>next</c> key.</param>
public sealed record SearchMovementsApiRequest(
    IReadOnlyList<string?>? Contracts,
    string? Currency = null,
    string? FromDate = null,
    string? ToDate = null,
    string? MoneyFlow = null,
    string? Keyword = null,
    int? PageSize = null,
    string? PaginationKey = null);

/// <summary>A page of movements.</summary>
/// <param name="Movements">The movements on this page.</param>
/// <param name="Pagination">Cursor metadata, absent when there were no results.</param>
/// <param name="InfoMessage">
/// The bank's informational message when the result set is empty, typically
/// <c>No transactions to show.</c> This is a successful outcome, not an error.
/// </param>
public sealed record MovementPageResponse(
    IReadOnlyList<MovementResponse> Movements,
    PaginationResponse? Pagination,
    string? InfoMessage)
{
    public static MovementPageResponse From(MovementPage page)
    {
        ArgumentNullException.ThrowIfNull(page);

        return new MovementPageResponse(
            [.. page.Movements.Select(MovementResponse.From)],
            page.Pagination is null ? null : PaginationResponse.From(page.Pagination),
            page.InfoMessage);
    }
}

/// <summary>A single account movement.</summary>
/// <param name="Id">Unique 31-character movement identifier.</param>
/// <param name="Comments">Description filled by the client or the originating system.</param>
/// <param name="IsFinanceable">Whether the movement can be financed.</param>
/// <param name="MoneyFlow">Either <c>INCOME</c> or <c>EXPENSE</c>.</param>
/// <param name="Status">Movement state, e.g. <c>SETTLED</c>. Echoed verbatim from BBVA.</param>
/// <param name="IsKnownStatus">
/// False when BBVA returned a state this client does not recognise. The raw value is still
/// present in <paramref name="Status"/>.
/// </param>
/// <param name="ValuationDate">When the movement becomes effective for interest.</param>
/// <param name="SignedAmount">
/// Movement amount signed for display: negative for expenses. BBVA always reports a
/// positive magnitude and encodes direction in <paramref name="MoneyFlow"/>.
/// </param>
/// <param name="Concept">Movement concept.</param>
/// <param name="PostedBalance">Contract balance after this movement was posted.</param>
/// <param name="Contract">Contract the movement belongs to.</param>
/// <param name="Operation">Bank-wide operation that triggered the movement.</param>
/// <param name="Transaction">Country-local transaction detail.</param>
public sealed record MovementResponse(
    string Id,
    string? Comments,
    bool IsFinanceable,
    string MoneyFlow,
    string? Status,
    bool IsKnownStatus,
    DateTimeOffset? ValuationDate,
    decimal SignedAmount,
    ConceptResponse Concept,
    MoneyResponse PostedBalance,
    ContractResponse Contract,
    OperationResponse Operation,
    TransactionResponse Transaction)
{
    public static MovementResponse From(Movement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        return new MovementResponse(
            movement.Id,
            movement.Comments,
            movement.IsFinanceable,
            MoneyFlowParser.ToWireValue(movement.Flow),
            movement.RawStatus,
            movement.Status is not null,
            movement.ValuationDate,
            movement.SignedAmount,
            new ConceptResponse(movement.Concept.Id, movement.Concept.Name),
            new MoneyResponse(movement.PostedBalance.Amount, movement.PostedBalance.Currency),
            new ContractResponse(movement.Contract.Id, movement.Contract.Number),
            new OperationResponse(movement.Operation.Id, movement.Operation.Date),
            new TransactionResponse(
                movement.Transaction.Id,
                movement.Transaction.Amount,
                movement.Transaction.Currency));
    }
}

/// <param name="Id">Concept identifier, e.g. <c>612</c>.</param>
/// <param name="Name">Concept name, e.g. <c>CARGO POR TRASPASO</c>.</param>
public sealed record ConceptResponse(string Id, string Name);

/// <param name="Amount">Monetary value.</param>
/// <param name="Currency">ISO-4217 code.</param>
public sealed record MoneyResponse(decimal Amount, string Currency);

/// <param name="Id">Global contract identifier.</param>
/// <param name="Number">The 18-digit account number.</param>
public sealed record ContractResponse(string Id, string Number);

/// <param name="Id">Global operation code.</param>
/// <param name="Date">When the client performed the operation.</param>
public sealed record OperationResponse(string Id, DateTimeOffset? Date);

/// <param name="Id">Country-local sequence number.</param>
/// <param name="Amount">Operation amount, always positive.</param>
/// <param name="Currency">ISO-4217 code.</param>
public sealed record TransactionResponse(string Id, decimal Amount, string Currency);

/// <param name="TotalPages">Total pages for the supplied filters.</param>
/// <param name="TotalElements">Total matching movements.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="First">Cursor for the first page.</param>
/// <param name="Last">Cursor for the last page.</param>
/// <param name="Previous">Cursor for the previous page.</param>
/// <param name="Next">Cursor for the next page, absent on the last page.</param>
/// <param name="HasNext">Whether a further page can be requested.</param>
public sealed record PaginationResponse(
    int? TotalPages,
    int? TotalElements,
    int? PageSize,
    string? First,
    string? Last,
    string? Previous,
    string? Next,
    bool HasNext)
{
    public static PaginationResponse From(PaginationInfo pagination)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        return new PaginationResponse(
            pagination.TotalPages,
            pagination.TotalElements,
            pagination.PageSize,
            pagination.Keys.First,
            pagination.Keys.Last,
            pagination.Keys.Previous,
            pagination.Keys.Next,
            pagination.Keys.HasNext);
    }
}

/// <summary>Request for a signature preview.</summary>
/// <param name="Method">HTTP method, e.g. <c>POST</c>.</param>
/// <param name="Path">Absolute API path, e.g. <c>/pe/business-reconciliation/v1/transactions/search</c>.</param>
/// <param name="Body">Request body, or null for bodyless requests.</param>
public sealed record SignaturePreviewApiRequest(string Method, string Path, string? Body);
