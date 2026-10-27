using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Domain.Tests.ValueObjects;

/// <summary>
/// Mirrors sandbox test plan cases DTL-12, DTL-14 and DTL-15.
/// </summary>
public sealed class TransactionIdTests
{
    [Theory]
    [InlineData("PE00110201300105109846000000014")]
    [InlineData("PE00110201300210484377000000353")]
    public void Accepts_thirty_one_alphanumeric_characters(string raw)
    {
        var result = TransactionId.Create(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_missing_value(string? raw)
    {
        var result = TransactionId.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("invalidParameters");
        result.Error.Message.ShouldBe("Mandatory parameter transaction-id not informed.");
    }

    [Theory]
    [InlineData("PE001102013002105154930000000066")]  // DTL-14: 32 characters
    [InlineData("PE0011020130021051549300000065")]    // DTL-15: 30 characters
    public void Rejects_wrong_length(string raw)
    {
        var result = TransactionId.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("The transaction number must be 31 characters.");
    }

    [Fact]
    public void Rejects_non_alphanumeric_when_length_is_correct()
    {
        // DTL-12: exactly 31 characters but contains ';', '*' and '='.
        var result = TransactionId.Create("PE0;110201300210515*930000=0065");

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("The transaction number only accepts letters and numbers.");
    }

    [Fact]
    public void Length_is_checked_before_charset()
    {
        var result = TransactionId.Create("PE0;110201300210515*930000=00655");

        result.Error.Message.ShouldBe("The transaction number must be 31 characters.");
    }
}
