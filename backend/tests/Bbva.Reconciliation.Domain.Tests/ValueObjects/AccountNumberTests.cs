using Bbva.Reconciliation.Domain.ValueObjects;

namespace Bbva.Reconciliation.Domain.Tests.ValueObjects;

/// <summary>
/// Error messages are asserted verbatim against the official BBVA sandbox test plan
/// (cases SRCH-12 through SRCH-15, DTL-08 through DTL-10). Rewording any of them means
/// the local fail-fast validation no longer mirrors what the bank actually returns.
/// </summary>
public sealed class AccountNumberTests
{
    [Theory]
    [InlineData("001101300210484377")]
    [InlineData("001101300105109846")]
    [InlineData("001109100100500997")]
    public void Accepts_eighteen_digit_numbers(string raw)
    {
        var result = AccountNumber.Create(raw);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Value.ShouldBe(raw);
        result.Value.ToString().ShouldBe(raw);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Rejects_missing_value(string? raw)
    {
        var result = AccountNumber.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("invalidParameters");
        result.Error.Message.ShouldBe("Mandatory parameter contracts not informed.");
    }

    [Theory]
    [InlineData("0011013001051180004")]   // SRCH-12: 19 digits
    [InlineData("00110130210522635")]     // SRCH-13: 17 digits
    [InlineData("00110130010511473")]     // DTL-09: 17 digits
    public void Rejects_wrong_length(string raw)
    {
        var result = AccountNumber.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("The account number must be 18 digits long.");
    }

    [Theory]
    [InlineData("001101300F0.1*9854")]    // SRCH-15: 18 chars, not all digits
    [InlineData("0*110LK00105114732")]    // DTL-10: 18 chars, not all digits
    public void Rejects_non_numeric_when_length_is_correct(string raw)
    {
        var result = AccountNumber.Create(raw);

        result.IsFailure.ShouldBeTrue();
        result.Error.Message.ShouldBe("The account number accepts only numbers.");
    }

    [Fact]
    public void Length_is_checked_before_charset()
    {
        // A 19-character non-numeric value reports the length problem, matching the
        // bank's precedence observed across SRCH-12 and SRCH-15.
        var result = AccountNumber.Create("0011013001051180ABC");

        result.Error.Message.ShouldBe("The account number must be 18 digits long.");
    }

    [Fact]
    public void Equality_is_by_value()
    {
        var a = AccountNumber.Create("001101300210484377").Value;
        var b = AccountNumber.Create("001101300210484377").Value;

        a.ShouldBe(b);
        a.GetHashCode().ShouldBe(b.GetHashCode());
    }
}
