using Bbva.Reconciliation.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;

namespace Bbva.Reconciliation.Domain.Tests.ValueObjects;

/// <summary>
/// Mirrors sandbox test plan cases SRCH-17 through SRCH-21, SRCH-23 and SRCH-24.
/// "Current date" is injected via <see cref="TimeProvider"/> so the upper-bound rule
/// stays deterministic instead of drifting with the wall clock.
/// </summary>
public sealed class DateRangeTests
{
    private static readonly DateTimeOffset Now = new(2024, 12, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly TimeProvider Clock = new FakeTimeProvider(Now);

    [Fact]
    public void Absent_range_is_valid_and_empty()
    {
        var result = DateRange.Create(null, null, Clock);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Theory]
    [InlineData("", "")]
    [InlineData("   ", null)]
    public void Blank_values_on_both_sides_are_treated_as_absent(string? from, string? to)
    {
        // SRCH-07 sends "operationDate": "" and still returns 200.
        var result = DateRange.Create(from, to, Clock);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeNull();
    }

    [Fact]
    public void Accepts_a_well_formed_range()
    {
        var result = DateRange.Create("2024-11-01T00:00:00.000Z", "2024-11-21T00:00:00.000Z", Clock);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.From.ShouldBe(new DateTimeOffset(2024, 11, 1, 0, 0, 0, TimeSpan.Zero));
        result.Value.To.ShouldBe(new DateTimeOffset(2024, 11, 21, 0, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void Rejects_missing_to_date()
    {
        // SRCH-20
        var result = DateRange.Create("2024-06-25T00:00:00.000Z", "", Clock);

        result.Error.Message.ShouldBe("Mandatory parameter operationDate.toDate not informed.");
    }

    [Fact]
    public void Rejects_missing_from_date()
    {
        // SRCH-21
        var result = DateRange.Create(null, "2024-11-18T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe("Mandatory parameter operationDate.fromDate not informed.");
    }

    [Fact]
    public void Rejects_malformed_from_date()
    {
        // SRCH-17
        var result = DateRange.Create("2024-10/01T00:00:00.000Z", "2024-11-21T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe(
            "Invalid format for parameter operationDate.fromDate. Correct format: yyyy-MM-ddTHH:mm:ss.SSSZ");
    }

    [Fact]
    public void Rejects_malformed_to_date()
    {
        // SRCH-18
        var result = DateRange.Create("2024-09-11T00:00:00.000Z", "2024/11-06T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe(
            "Invalid format for parameter operationDate.toDate. Correct format: yyyy-MM-ddTHH:mm:ss.SSSZ");
    }

    [Fact]
    public void Rejects_from_date_before_the_supported_epoch()
    {
        // SRCH-23
        var result = DateRange.Create("2023-12-31T00:00:00.000Z", "2024-11-20T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe(
            "The parameter operationDate.fromDate must be later than or equal to the date 2024-01-01T00:00:00.000Z");
    }

    [Fact]
    public void Accepts_the_supported_epoch_boundary()
    {
        var result = DateRange.Create("2024-01-01T00:00:00.000Z", "2024-11-20T00:00:00.000Z", Clock);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Rejects_to_date_in_the_future()
    {
        // SRCH-24
        var result = DateRange.Create("2024-04-20T00:00:00.000Z", "2030-12-26T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe(
            "The parameter operationDate.toDate must be before than or equal to the current date.");
    }

    [Fact]
    public void Rejects_to_date_earlier_than_from_date()
    {
        // SRCH-19
        var result = DateRange.Create("2024-11-10T00:00:00.000Z", "2024-11-01T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe(
            "The parameter operationDate.toDate value must be later than operationDate.fromDate value.");
    }

    [Fact]
    public void Presence_is_checked_before_format()
    {
        var result = DateRange.Create("not-a-date", "", Clock);

        result.Error.Message.ShouldBe("Mandatory parameter operationDate.toDate not informed.");
    }

    [Fact]
    public void Format_is_checked_before_bounds()
    {
        var result = DateRange.Create("1999/01/01", "2024-11-20T00:00:00.000Z", Clock);

        result.Error.Message.ShouldBe(
            "Invalid format for parameter operationDate.fromDate. Correct format: yyyy-MM-ddTHH:mm:ss.SSSZ");
    }

    [Fact]
    public void Round_trips_to_the_wire_format()
    {
        var range = DateRange.Create("2024-11-01T00:00:00.000Z", "2024-11-21T00:00:00.000Z", Clock).Value!;

        range.FromWireValue.ShouldBe("2024-11-01T00:00:00.000Z");
        range.ToWireValue.ShouldBe("2024-11-21T00:00:00.000Z");
    }
}
