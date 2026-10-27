using System.Globalization;
using Bbva.Reconciliation.Domain.Movements;
using Bbva.Reconciliation.Domain.ValueObjects;
using Bbva.Reconciliation.Infrastructure.Bbva.Contracts;

namespace Bbva.Reconciliation.Infrastructure.Bbva.Mapping;

/// <summary>
/// Translates BBVA wire objects into the domain model.
/// </summary>
/// <remarks>
/// The mapper is deliberately forgiving. Every observed sandbox response is well formed,
/// but a single unexpected null in one movement must not discard the other forty-nine on
/// the page; defaults are substituted and the raw value is preserved where it matters
/// (see <see cref="Movement.RawStatus"/>).
/// </remarks>
internal static class MovementMapper
{
    /// <summary>
    /// Timestamps arrive as <c>2024-10-21T22:19:00.000-0500</c>: an ISO-8601 offset with
    /// no colon, which round-trip parsing rejects. The explicit patterns cover that form
    /// plus the colon and Z variants.
    /// </summary>
    private static readonly string[] TimestampFormats =
    [
        "yyyy-MM-dd'T'HH:mm:ss.fffzzz",
        "yyyy-MM-dd'T'HH:mm:ss.fffK",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ssK",
        "yyyy-MM-dd'T'HH:mm:ss.fff",
    ];

    public static MovementPage ToPage(SearchResponseDto dto)
    {
        if (dto.Movements is not { Count: > 0 })
        {
            return MovementPage.Empty(FirstInfoMessage(dto.Messages));
        }

        var movements = new List<Movement>(dto.Movements.Count);

        foreach (var movement in dto.Movements)
        {
            movements.Add(ToMovement(movement));
        }

        return new MovementPage(movements, ToPagination(dto.Pagination), FirstInfoMessage(dto.Messages));
    }

    public static Movement ToMovement(MovementDto dto) =>
        new(
            Id: dto.Id ?? string.Empty,
            Comments: dto.Comments,
            IsFinanceable: dto.IsFinanceable ?? false,
            Flow: ParseFlow(dto.MoneyFlow),
            Status: TransactionStatusParser.Parse(dto.Status),
            RawStatus: dto.Status,
            ValuationDate: ParseTimestamp(dto.ValuationDate),
            Concept: new Concept(dto.Concept?.Id ?? string.Empty, dto.Concept?.Name ?? string.Empty),
            PostedBalance: new Money(dto.PostedBalance?.Amount ?? 0m, dto.PostedBalance?.Currency ?? string.Empty),
            Contract: new ContractRef(dto.Contract?.Id ?? string.Empty, dto.Contract?.Number ?? string.Empty),
            Operation: new OperationRef(dto.Operation?.Id ?? string.Empty, ParseTimestamp(dto.Operation?.Date)),
            Transaction: new TransactionRef(
                dto.Transaction?.Id ?? string.Empty,
                dto.Transaction?.Amount ?? 0m,
                dto.Transaction?.Currency ?? string.Empty));

    /// <summary>
    /// Defaults to <see cref="MoneyFlow.Income"/> only as a last resort. The field is
    /// mandatory in the documentation and present in every observed response, so an
    /// unparseable value means the contract changed.
    /// </summary>
    private static MoneyFlow ParseFlow(string? raw)
    {
        var parsed = MoneyFlowParser.Parse(raw);
        return parsed.IsSuccess ? parsed.Value : MoneyFlow.Income;
    }

    private static PaginationInfo? ToPagination(PaginationDto? dto) =>
        dto is null
            ? null
            : new PaginationInfo(
                dto.TotalPages,
                dto.TotalElements,
                dto.PageSize,
                new PaginationKeys(
                    Blank(dto.Keys?.First),
                    Blank(dto.Keys?.Last),
                    Blank(dto.Keys?.Previous),
                    Blank(dto.Keys?.Next)));

    private static DateTimeOffset? ParseTimestamp(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        if (DateTimeOffset.TryParseExact(
                raw,
                TimestampFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal,
                out var exact))
        {
            return exact;
        }

        return DateTimeOffset.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var loose)
            ? loose
            : null;
    }

    private static string? FirstInfoMessage(IReadOnlyList<MessageDto>? messages) =>
        messages?.FirstOrDefault(message =>
            string.Equals(message.Type, "INFO", StringComparison.OrdinalIgnoreCase))?.Message;

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
