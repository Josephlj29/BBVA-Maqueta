using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Domain.Movements;

/// <summary>A single account movement as returned by the Business Reconciliation API.</summary>
/// <param name="Id">Unique 31-character movement identifier.</param>
/// <param name="Comments">Free-text description filled by the client or the originating system.</param>
/// <param name="IsFinanceable">Whether the movement can be financed.</param>
/// <param name="Flow">Whether the movement adds to or subtracts from the balance.</param>
/// <param name="Status">Movement state. Null when the bank returned a value this client does not know.</param>
/// <param name="RawStatus">The status exactly as received, preserved even when unrecognised.</param>
/// <param name="ValuationDate">When the debit or credit becomes effective and starts accruing interest.</param>
/// <param name="Concept">Movement concept.</param>
/// <param name="PostedBalance">Contract balance after this movement was posted.</param>
/// <param name="Contract">Contract the movement belongs to.</param>
/// <param name="Operation">Bank-wide operation that triggered the movement.</param>
/// <param name="Transaction">Country-local transaction detail.</param>
public sealed record Movement(
    string Id,
    string? Comments,
    bool IsFinanceable,
    MoneyFlow Flow,
    TransactionStatus? Status,
    string? RawStatus,
    DateTimeOffset? ValuationDate,
    Concept Concept,
    Money PostedBalance,
    ContractRef Contract,
    OperationRef Operation,
    TransactionRef Transaction)
{
    /// <summary>
    /// The movement amount signed for display: negative for expenses, positive for income.
    /// The API always reports a positive magnitude and encodes direction in <c>moneyFlow</c>.
    /// </summary>
    public decimal SignedAmount =>
        Flow == MoneyFlow.Expense ? -Transaction.Amount : Transaction.Amount;
}

/// <param name="Id">Concept identifier, e.g. <c>612</c>.</param>
/// <param name="Name">Concept name, e.g. <c>CARGO POR TRASPASO</c>.</param>
public sealed record Concept(string Id, string Name);

/// <param name="Amount">Monetary value.</param>
/// <param name="Currency">ISO-4217 code, e.g. <c>PEN</c> or <c>USD</c>.</param>
public sealed record Money(decimal Amount, string Currency);

/// <param name="Id">Global contract identifier, e.g. <c>PE001101300210484377</c>.</param>
/// <param name="Number">The 18-digit account number.</param>
public sealed record ContractRef(string Id, string Number);

/// <param name="Id">Global operation code linking related transactions.</param>
/// <param name="Date">When the client performed the operation.</param>
public sealed record OperationRef(string Id, DateTimeOffset? Date);

/// <param name="Id">Country-local sequence number, e.g. <c>353</c>.</param>
/// <param name="Amount">Operation amount, always a positive magnitude.</param>
/// <param name="Currency">ISO-4217 code of the operation.</param>
public sealed record TransactionRef(string Id, decimal Amount, string Currency);
