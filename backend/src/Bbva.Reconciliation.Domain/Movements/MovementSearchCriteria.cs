using Bbva.Reconciliation.Domain.Errors;
using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Domain.Movements;

/// <summary>
/// A validated <c>POST /transactions/search</c> request. Constructing one is the only way
/// to reach the BBVA client, so an invalid search cannot leave this process.
/// </summary>
public sealed class MovementSearchCriteria
{
    private MovementSearchCriteria(
        AccountNumber contract,
        string? currency,
        DateRange? operationDate,
        MoneyFlow? flow,
        string? keyword,
        int? pageSize,
        string? paginationKey)
    {
        Contract = contract;
        Currency = currency;
        OperationDate = operationDate;
        Flow = flow;
        Keyword = keyword;
        PageSize = pageSize;
        PaginationKey = paginationKey;
    }

    public AccountNumber Contract { get; }

    /// <summary>ISO-4217 filter for <c>transactionAmount.currency</c>.</summary>
    public string? Currency { get; }

    public DateRange? OperationDate { get; }

    public MoneyFlow? Flow { get; }

    /// <summary>Free-text filter matched against the movement description.</summary>
    public string? Keyword { get; }

    public int? PageSize { get; }

    /// <summary>Opaque cursor from a previous page's <c>pagination.keys.next</c>.</summary>
    public string? PaginationKey { get; }

    /// <summary>
    /// Validates every field in the order the bank reports problems, so the first message
    /// a caller sees is the one BBVA itself would have returned.
    /// </summary>
    /// <param name="contracts">
    /// The raw contract list. BBVA accepts exactly one entry; more than one is rejected
    /// with "The number of contracts must be 1." (SRCH-11).
    /// </param>
    public static Result<MovementSearchCriteria> Create(
        IReadOnlyList<string?>? contracts,
        string? currency,
        string? fromDate,
        string? toDate,
        string? moneyFlow,
        string? keyword,
        int? pageSize,
        string? paginationKey,
        TimeProvider clock)
    {
        if (contracts is null || contracts.Count == 0)
        {
            return DomainError.Invalid("Mandatory parameter contracts not informed.");
        }

        if (contracts.Count > 1)
        {
            return DomainError.Invalid("The number of contracts must be 1.");
        }

        var contract = AccountNumber.Create(contracts[0]);
        if (contract.IsFailure)
        {
            return Result<MovementSearchCriteria>.Failure(contract.Error);
        }

        var operationDate = DateRange.Create(fromDate, toDate, clock);
        if (operationDate.IsFailure)
        {
            return Result<MovementSearchCriteria>.Failure(operationDate.Error);
        }

        var flow = MoneyFlowParser.ParseOptional(moneyFlow);
        if (flow.IsFailure)
        {
            return Result<MovementSearchCriteria>.Failure(flow.Error);
        }

        if (pageSize is <= 0)
        {
            return DomainError.Invalid("The page size must be greater than zero.");
        }

        return new MovementSearchCriteria(
            contract.Value,
            Blank(currency),
            operationDate.Value,
            flow.Value,
            Blank(keyword),
            pageSize,
            Blank(paginationKey));
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
