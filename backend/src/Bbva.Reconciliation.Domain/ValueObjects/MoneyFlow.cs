using Bbva.Reconciliation.Domain.Errors;

namespace Bbva.Reconciliation.Domain.ValueObjects;

/// <summary>Whether a movement adds to or subtracts from the contract balance.</summary>
public enum MoneyFlow
{
    /// <summary>Cash flow received by the product contract.</summary>
    Income,

    /// <summary>Cash flow taken from the product contract.</summary>
    Expense,
}

public static class MoneyFlowParser
{
    private const string Income = "INCOME";
    private const string Expense = "EXPENSE";

    public static string ToWireValue(MoneyFlow flow) => flow switch
    {
        MoneyFlow.Income => Income,
        MoneyFlow.Expense => Expense,
        _ => throw new ArgumentOutOfRangeException(nameof(flow), flow, "Unknown money flow."),
    };

    /// <summary>Parses a required value. The bank's enum is case sensitive.</summary>
    public static Result<MoneyFlow> Parse(string? raw) => raw switch
    {
        Income => MoneyFlow.Income,
        Expense => MoneyFlow.Expense,
        _ => DomainError.Invalid("Invalid moneyFlow value. Admitted: EXPENSE, INCOME."),
    };

    /// <summary>
    /// Parses an optional filter. Blank means "no filter" — SRCH-07 sends
    /// <c>"moneyFlow": ""</c> and the bank answers 200.
    /// </summary>
    public static Result<MoneyFlow?> ParseOptional(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Result<MoneyFlow?>.Success(null);
        }

        var parsed = Parse(raw);

        return parsed.IsSuccess
            ? Result<MoneyFlow?>.Success(parsed.Value)
            : Result<MoneyFlow?>.Failure(parsed.Error);
    }
}
