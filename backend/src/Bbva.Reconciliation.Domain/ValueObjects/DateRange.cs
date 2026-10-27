using System.Globalization;
using Bbva.Reconciliation.Domain.Errors;

namespace Bbva.Reconciliation.Domain.ValueObjects;

/// <summary>
/// The <c>operationDate</c> filter of <c>POST /transactions/search</c>. Both bounds are
/// mandatory once either is supplied, and the bank restricts the window to dates from
/// 2024-01-01 up to today.
/// </summary>
public sealed class DateRange
{
    /// <summary>The wire format BBVA quotes in its own error messages.</summary>
    public const string WireFormat = "yyyy-MM-ddTHH:mm:ss.fffZ";

    /// <summary>Earliest date the API will accept for <c>fromDate</c>.</summary>
    public static readonly DateTimeOffset MinimumFromDate =
        new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private DateRange(DateTimeOffset from, DateTimeOffset to)
    {
        From = from;
        To = to;
    }

    public DateTimeOffset From { get; }

    public DateTimeOffset To { get; }

    public string FromWireValue => Format(From);

    public string ToWireValue => Format(To);

    public static string Format(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(WireFormat, CultureInfo.InvariantCulture);

    /// <summary>
    /// Builds the filter, or <c>null</c> when neither bound was supplied — an absent
    /// <c>operationDate</c> is valid and simply means "no date filter".
    /// </summary>
    /// <remarks>
    /// <paramref name="clock"/> supplies "today" for the upper-bound rule so the check
    /// is deterministic under test instead of depending on the wall clock.
    /// </remarks>
    public static Result<DateRange?> Create(string? fromDate, string? toDate, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(clock);

        var hasFrom = !string.IsNullOrWhiteSpace(fromDate);
        var hasTo = !string.IsNullOrWhiteSpace(toDate);

        if (!hasFrom && !hasTo)
        {
            return Result<DateRange?>.Success(null);
        }

        // Presence before format: SRCH-20 sends a malformed fromDate with an empty
        // toDate and the bank still reports the missing toDate.
        if (!hasTo)
        {
            return DomainError.Invalid("Mandatory parameter operationDate.toDate not informed.");
        }

        if (!hasFrom)
        {
            return DomainError.Invalid("Mandatory parameter operationDate.fromDate not informed.");
        }

        if (!TryParse(fromDate!, out var from))
        {
            return DomainError.Invalid(
                "Invalid format for parameter operationDate.fromDate. Correct format: yyyy-MM-ddTHH:mm:ss.SSSZ");
        }

        if (!TryParse(toDate!, out var to))
        {
            return DomainError.Invalid(
                "Invalid format for parameter operationDate.toDate. Correct format: yyyy-MM-ddTHH:mm:ss.SSSZ");
        }

        if (from < MinimumFromDate)
        {
            return DomainError.Invalid(
                "The parameter operationDate.fromDate must be later than or equal to the date 2024-01-01T00:00:00.000Z");
        }

        if (to > clock.GetUtcNow())
        {
            return DomainError.Invalid(
                "The parameter operationDate.toDate must be before than or equal to the current date.");
        }

        if (to <= from)
        {
            return DomainError.Invalid(
                "The parameter operationDate.toDate value must be later than operationDate.fromDate value.");
        }

        return Result<DateRange?>.Success(new DateRange(from, to));
    }

    private static bool TryParse(string raw, out DateTimeOffset value) =>
        DateTimeOffset.TryParseExact(
            raw.Trim(),
            ["yyyy-MM-dd'T'HH:mm:ss.fff'Z'", "yyyy-MM-dd'T'HH:mm:ss.fffK", "yyyy-MM-dd'T'HH:mm:ssK"],
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out value);
}
