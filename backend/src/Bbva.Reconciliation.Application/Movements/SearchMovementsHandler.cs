using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Domain.Errors;
using Bbva.Reconciliation.Domain.Movements;

namespace Bbva.Reconciliation.Application.Movements;

/// <summary>
/// The raw, unvalidated shape of a movement search, mirroring the BBVA request body so a
/// caller can paste a sandbox test-plan body straight through.
/// </summary>
/// <param name="Contracts">Contract numbers. BBVA accepts exactly one.</param>
/// <param name="Currency">Optional ISO-4217 filter.</param>
/// <param name="FromDate">Optional lower bound, ISO-8601.</param>
/// <param name="ToDate">Optional upper bound, ISO-8601.</param>
/// <param name="MoneyFlow">Optional INCOME or EXPENSE filter.</param>
/// <param name="Keyword">Optional free-text filter.</param>
/// <param name="PageSize">Optional page size.</param>
/// <param name="PaginationKey">Opaque cursor from a previous page.</param>
public sealed record SearchMovementsRequest(
    IReadOnlyList<string?>? Contracts,
    string? Currency = null,
    string? FromDate = null,
    string? ToDate = null,
    string? MoneyFlow = null,
    string? Keyword = null,
    int? PageSize = null,
    string? PaginationKey = null);

/// <summary>
/// Validates a search locally before spending a network round trip, then delegates to the
/// BBVA adapter.
/// </summary>
/// <remarks>
/// The local rules reproduce the bank's own messages verbatim. That is not duplication for
/// its own sake: it turns an 800 ms round trip into instant feedback in the UI, and it
/// keeps a malformed request from consuming the caller's API quota.
/// </remarks>
public sealed class SearchMovementsHandler(IBbvaTransactionsPort transactions, TimeProvider clock)
{
    private readonly IBbvaTransactionsPort _transactions =
        transactions ?? throw new ArgumentNullException(nameof(transactions));

    private readonly TimeProvider _clock = clock ?? throw new ArgumentNullException(nameof(clock));

    public async Task<Result<MovementPage>> HandleAsync(
        SearchMovementsRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var criteria = MovementSearchCriteria.Create(
            request.Contracts,
            request.Currency,
            request.FromDate,
            request.ToDate,
            request.MoneyFlow,
            request.Keyword,
            request.PageSize,
            request.PaginationKey,
            _clock);

        if (criteria.IsFailure)
        {
            return Result<MovementPage>.Failure(criteria.Error);
        }

        return await _transactions.SearchAsync(criteria.Value, cancellationToken).ConfigureAwait(false);
    }
}
