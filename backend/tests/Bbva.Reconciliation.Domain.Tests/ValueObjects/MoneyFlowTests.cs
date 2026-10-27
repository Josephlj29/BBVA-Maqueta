using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Domain.Tests.ValueObjects;

public sealed class MoneyFlowTests
{
    [Theory]
    [InlineData("INCOME", MoneyFlow.Income)]
    [InlineData("EXPENSE", MoneyFlow.Expense)]
    public void Parses_the_admitted_values(string raw, MoneyFlow expected)
    {
        var result = MoneyFlowParser.Parse(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(expected);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Treats_blank_as_no_filter(string? raw)
    {
        // SRCH-07 sends "moneyFlow": "" and still returns 200.
        var result = MoneyFlowParser.ParseOptional(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("ENTRY")]    // SRCH-22
    [InlineData("income")]   // the bank's enum is case sensitive
    [InlineData("INGRESO")]
    public void Rejects_anything_else(string raw)
    {
        var result = MoneyFlowParser.Parse(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("invalidParameters");
        result.Error.Message.ShouldBe("Invalid moneyFlow value. Admitted: EXPENSE, INCOME.");
    }

    [Theory]
    [InlineData(MoneyFlow.Income, "INCOME")]
    [InlineData(MoneyFlow.Expense, "EXPENSE")]
    public void Serializes_back_to_the_wire_value(MoneyFlow flow, string expected)
    {
        MoneyFlowParser.ToWireValue(flow).ShouldBe(expected);
    }
}
