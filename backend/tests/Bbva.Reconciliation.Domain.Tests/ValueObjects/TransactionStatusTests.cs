using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Domain.Tests.ValueObjects;

public sealed class TransactionStatusTests
{
    /// <summary>The eleven states listed in the technical documentation, section "status".</summary>
    private static readonly string[] DocumentedValues =
    [
        "SETTLED", "PENDING", "SENT", "RETURNED", "REJECTED", "AUTHORIZATION_PENDING",
        "CANCELED", "REFUSED", "REFUND", "CHARGEBACKED", "CHARGED",
    ];

    [Fact]
    public void Every_documented_value_round_trips()
    {
        foreach (var raw in DocumentedValues)
        {
            var parsed = TransactionStatusParser.Parse(raw);

            parsed.ShouldNotBeNull($"'{raw}' is documented and must parse");
            TransactionStatusParser.ToWireValue(parsed.Value).ShouldBe(raw);
        }
    }

    [Fact]
    public void Enum_declares_exactly_the_documented_values()
    {
        Enum.GetValues<TransactionStatus>().Length.ShouldBe(DocumentedValues.Length);
    }

    [Theory]
    [InlineData("LIQUIDADO")]
    [InlineData("settled")]
    [InlineData("")]
    public void Unknown_values_parse_to_null_rather_than_throwing(string raw)
    {
        // The bank may introduce states before this client is updated; an unrecognised
        // status must never take down a whole page of movements.
        TransactionStatusParser.Parse(raw).ShouldBeNull();
    }
}
