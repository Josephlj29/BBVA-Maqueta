using System.Diagnostics.CodeAnalysis;
using Bbva.Reconciliation.Domain.Errors;

namespace Bbva.Reconciliation.Domain.ValueObjects;

/// <summary>
/// An 18-digit BBVA Peru account number, used both as a contract identifier in
/// <c>POST /transactions/search</c> and as the <c>account-id</c> path segment in
/// <c>GET /accounts/{account-id}/transactions/{transaction-id}</c>.
/// </summary>
public readonly record struct AccountNumber
{
    public const int RequiredLength = 18;

    private AccountNumber(string value) => Value = value;

    public string Value { get; }

    public override string ToString() => Value;

    /// <summary>
    /// Validates in the same order the bank does: presence, then length, then charset.
    /// A 19-character non-numeric value reports the length problem, not the charset one.
    /// </summary>
    public static Result<AccountNumber> Create(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return DomainError.Invalid("Mandatory parameter contracts not informed.");
        }

        var value = raw.Trim();

        if (value.Length != RequiredLength)
        {
            return DomainError.Invalid("The account number must be 18 digits long.");
        }

        if (!IsAllDigits(value))
        {
            return DomainError.Invalid("The account number accepts only numbers.");
        }

        return new AccountNumber(value);
    }

    public static bool TryCreate(string? raw, [NotNullWhen(true)] out AccountNumber? account)
    {
        var result = Create(raw);
        account = result.IsSuccess ? result.Value : null;
        return result.IsSuccess;
    }

    private static bool IsAllDigits(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsAsciiDigit(character))
            {
                return false;
            }
        }

        return true;
    }
}
