using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Domain.Errors;
using Bbva.Reconciliation.Domain.Movements;
using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Application.Movements;

/// <summary>
/// Retrieves one movement, validating the account and transaction identifiers before
/// putting them into a URL.
/// </summary>
/// <remarks>
/// Validating first is what makes the signed path trustworthy. The reference WinForms
/// client appended path parameters after computing the signature, so a detail request was
/// signed for a different target than the one it hit; here the identifiers are proven
/// well formed and the path is built once, before signing.
/// </remarks>
public sealed class GetMovementHandler(IBbvaTransactionsPort transactions)
{
    private readonly IBbvaTransactionsPort _transactions =
        transactions ?? throw new ArgumentNullException(nameof(transactions));

    public async Task<Result<Movement>> HandleAsync(
        string? accountId,
        string? transactionId,
        CancellationToken cancellationToken = default)
    {
        var account = AccountNumber.Create(accountId);

        if (account.IsFailure)
        {
            return Result<Movement>.Failure(account.Error);
        }

        var transaction = TransactionId.Create(transactionId);

        if (transaction.IsFailure)
        {
            return Result<Movement>.Failure(transaction.Error);
        }

        return await _transactions
            .GetMovementAsync(account.Value, transaction.Value, cancellationToken)
            .ConfigureAwait(false);
    }
}
