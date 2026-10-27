using System.Text.Json.Serialization;

namespace Bbva.Reconciliation.Infrastructure.Bbva.Contracts;

/// <summary>
/// Wire shapes for the BBVA Business Reconciliation API, kept deliberately separate from
/// the domain model. The bank's JSON is denormalised and uses its own casing; letting it
/// leak inward would tie the domain to a contract this project does not own.
/// </summary>
/// <remarks>
/// Every property is nullable. The documentation marks most fields mandatory, but the
/// sandbox returns partial objects in several error and edge cases, and a null reference
/// while deserialising a page of movements is not a useful failure mode.
/// </remarks>
internal sealed record SearchRequestDto
{
    [JsonPropertyName("contracts")]
    public required IReadOnlyList<string> Contracts { get; init; }

    [JsonPropertyName("transactionAmount")]
    public TransactionAmountFilterDto? TransactionAmount { get; init; }

    [JsonPropertyName("operationDate")]
    public OperationDateFilterDto? OperationDate { get; init; }

    [JsonPropertyName("moneyFlow")]
    public string? MoneyFlow { get; init; }

    [JsonPropertyName("keyword")]
    public string? Keyword { get; init; }

    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }

    [JsonPropertyName("paginationKey")]
    public string? PaginationKey { get; init; }
}

internal sealed record TransactionAmountFilterDto
{
    [JsonPropertyName("currency")]
    public required string Currency { get; init; }
}

internal sealed record OperationDateFilterDto
{
    [JsonPropertyName("fromDate")]
    public required string FromDate { get; init; }

    [JsonPropertyName("toDate")]
    public required string ToDate { get; init; }
}

/// <summary>
/// A search response. The bank returns either <c>movements</c> plus <c>pagination</c>, or
/// a <c>messages</c> array — the latter both for errors and, with type INFO, for an empty
/// result set.
/// </summary>
internal sealed record SearchResponseDto
{
    [JsonPropertyName("movements")]
    public IReadOnlyList<MovementDto>? Movements { get; init; }

    [JsonPropertyName("pagination")]
    public PaginationDto? Pagination { get; init; }

    [JsonPropertyName("messages")]
    public IReadOnlyList<MessageDto>? Messages { get; init; }
}

internal record MovementDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("comments")]
    public string? Comments { get; init; }

    [JsonPropertyName("isFinanceable")]
    public bool? IsFinanceable { get; init; }

    [JsonPropertyName("moneyFlow")]
    public string? MoneyFlow { get; init; }

    [JsonPropertyName("status")]
    public string? Status { get; init; }

    [JsonPropertyName("valuationDate")]
    public string? ValuationDate { get; init; }

    [JsonPropertyName("concept")]
    public ConceptDto? Concept { get; init; }

    [JsonPropertyName("postedBalance")]
    public AmountDto? PostedBalance { get; init; }

    [JsonPropertyName("contract")]
    public ContractDto? Contract { get; init; }

    [JsonPropertyName("operation")]
    public OperationDto? Operation { get; init; }

    [JsonPropertyName("transaction")]
    public TransactionDto? Transaction { get; init; }
}

internal sealed record ConceptDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

internal sealed record AmountDto
{
    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

internal sealed record ContractDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("number")]
    public string? Number { get; init; }
}

internal sealed record OperationDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("date")]
    public string? Date { get; init; }
}

internal sealed record TransactionDto
{
    [JsonPropertyName("id")]
    public string? Id { get; init; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; init; }

    [JsonPropertyName("currency")]
    public string? Currency { get; init; }
}

internal sealed record PaginationDto
{
    [JsonPropertyName("totalPages")]
    public int? TotalPages { get; init; }

    [JsonPropertyName("totalElements")]
    public int? TotalElements { get; init; }

    [JsonPropertyName("pageSize")]
    public int? PageSize { get; init; }

    [JsonPropertyName("keys")]
    public PaginationKeysDto? Keys { get; init; }
}

internal sealed record PaginationKeysDto
{
    [JsonPropertyName("first")]
    public string? First { get; init; }

    [JsonPropertyName("last")]
    public string? Last { get; init; }

    [JsonPropertyName("previous")]
    public string? Previous { get; init; }

    [JsonPropertyName("next")]
    public string? Next { get; init; }
}

/// <summary>
/// One entry of the bank's uniform <c>messages</c> envelope. <c>type</c> is ERROR for
/// failures and INFO for the "No transactions to show." empty-result case.
/// </summary>
internal sealed record MessageDto
{
    [JsonPropertyName("code")]
    public string? Code { get; init; }

    [JsonPropertyName("message")]
    public string? Message { get; init; }

    [JsonPropertyName("type")]
    public string? Type { get; init; }
}

/// <summary>A movement detail response: either the movement itself, or a messages envelope.</summary>
internal sealed record MovementDetailResponseDto : MovementDto
{
    [JsonPropertyName("messages")]
    public IReadOnlyList<MessageDto>? Messages { get; init; }
}
