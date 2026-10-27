using System.Diagnostics.CodeAnalysis;
using Bbva.Reconciliation.Domain.Errors;

namespace Bbva.Reconciliation.Domain.ValueObjects;

/// <summary>
/// A 31-character alphanumeric movement identifier, e.g.
/// <c>PE00110201300210484377000000353</c>. Note this is the value returned as
/// <c>id</c> and <c>operation.id</c>; the shorter <c>transaction.id</c> is a
/// different, per-account sequence number.
/// </summary>
public readonly record struct TransactionId
{
    public const int RequiredLength = 31;

    private TransactionId(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    /// <summary>Validates presence, then length, then charset — the bank's own precedence.</summary>
    public static Result<TransactionId> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DomainError.Invalid("Mandatory parameter transaction-id not informed.");
        }

        var value = raw.Trim();

        if (value.Length != RequiredLength)
        {
            return DomainError.Invalid("The transaction number must be 31 characters.");
        }

        if (!IsAlphanumeric(value))
        {
            return DomainError.Invalid("The transaction number only accepts letters and numbers.");
        }

        return new TransactionId(value);
    }

    public static bool TryCreate(string? raw, [NotNullWhen(true)] out TransactionId? transaction)
    {
        var result = Create(raw);
        transaction = result.IsSuccess ? result.Value : null;
        return result.IsSuccess;
    }

    private static bool IsAlphanumeric(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiLetterOrDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
