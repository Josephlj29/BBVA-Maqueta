using Bbva.Reconciliation.Domain.Errors;
using Bbva.Reconciliation.Domain.Movements;
using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Application.Ports;

/// <summary>
/// The driven port onto BBVA's Business Reconciliation API.
/// </summary>
/// <remarks>
/// Both operations take already-validated value objects, so an invalid request cannot
/// reach the adapter. Failures that only the bank can determine — access denied, unknown
/// transaction, service unavailable — come back as <see cref="DomainError"/> rather than
/// exceptions, because they are documented outcomes of the sandbox test plan.
/// </remarks>
public interface IBbvaTransactionsPort
{
    /// <summary>Lists movements for one contract. An empty page is a success, not a failure.</summary>
    Task<Result<MovementPage>> SearchAsync(
        MovementSearchCriteria criteria,
        CancellationToken cancellationToken = default);

    /// <summary>Retrieves a single movement by account and transaction identifier.</summary>
    Task<Result<Movement>> GetMovementAsync(
        AccountNumber account,
        TransactionId transaction,
        CancellationToken cancellationToken = default);
}
