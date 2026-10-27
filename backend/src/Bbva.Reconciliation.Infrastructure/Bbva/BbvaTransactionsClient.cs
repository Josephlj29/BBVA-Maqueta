using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Domain.Errors;
using Bbva.Reconciliation.Domain.Movements;
using Bbva.Reconciliation.Domain.ValueObjects;
using Bbva.Reconciliation.Infrastructure.Bbva.Contracts;
using Bbva.Reconciliation.Infrastructure.Bbva.Mapping;
using Microsoft.Extensions.Logging;

namespace Bbva.Reconciliation.Infrastructure.Bbva;

/// <summary>
/// Typed <see cref="HttpClient"/> adapter for the Business Reconciliation API.
/// </summary>
/// <remarks>
/// Authentication and message signing are not this class's concern: they are applied by
/// the signing handler in the pipeline, so the code here reads as plain HTTP calls.
/// </remarks>
public sealed class BbvaTransactionsClient : IBbvaTransactionsPort
{
    /// <summary>Base path of the API, appended to the API Market root.</summary>
    public const string ApiBasePath = "/pe/business-reconciliation/v1";

    /// <summary>Path of the movement search endpoint.</summary>
    public const string SearchPath = ApiBasePath + "/transactions/search";

    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<BbvaTransactionsClient> _logger;

    public BbvaTransactionsClient(HttpClient httpClient, ILogger<BbvaTransactionsClient> logger)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(logger);

        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Builds the movement detail path for the given identifiers.</summary>
    public static string DetailPath(AccountNumber account, TransactionId transaction) =>
        $"{ApiBasePath}/accounts/{account.Value}/transactions/{transaction.Value}";

    public async Task<Result<MovementPage>> SearchAsync(
        MovementSearchCriteria criteria,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        var payload = ToRequestDto(criteria);

        using var response = await _httpClient
            .PostAsJsonAsync(SearchPath, payload, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return Result<MovementPage>.Failure(ToError(response.StatusCode, body));
        }

        var dto = Deserialize<SearchResponseDto>(body);

        if (dto is null)
        {
            return Result<MovementPage>.Failure(
                DomainError.Bad("BBVA returned a successful status with an unreadable body."));
        }

        // A 200 can still carry an ERROR envelope; the sandbox does this for the
        // service-unavailable case (SRCH-25).
        if (FindError(dto.Messages) is { } embedded)
        {
            return Result<MovementPage>.Failure(embedded);
        }

        return Result<MovementPage>.Success(MovementMapper.ToPage(dto));
    }

    public async Task<Result<Movement>> GetMovementAsync(
        AccountNumber account,
        TransactionId transaction,
        CancellationToken cancellationToken = default)
    {
        var path = DetailPath(account, transaction);

        using var response = await _httpClient
            .GetAsync(path, cancellationToken)
            .ConfigureAwait(false);

        var body = await ReadBodyAsync(response, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            return Result<Movement>.Failure(ToError(response.StatusCode, body));
        }

        var dto = Deserialize<MovementDetailResponseDto>(body);

        if (dto is null)
        {
            return Result<Movement>.Failure(
                DomainError.Bad("BBVA returned a successful status with an unreadable body."));
        }

        if (FindError(dto.Messages) is { } embedded)
        {
            return Result<Movement>.Failure(embedded);
        }

        if (string.IsNullOrWhiteSpace(dto.Id))
        {
            return Result<Movement>.Failure(DomainError.Bad("The transaction number does not exist."));
        }

        return Result<Movement>.Success(MovementMapper.ToMovement(dto));
    }

    private static SearchRequestDto ToRequestDto(MovementSearchCriteria criteria) => new()
    {
        Contracts = [criteria.Contract.Value],
        TransactionAmount = criteria.Currency is null
            ? null
            : new TransactionAmountFilterDto { Currency = criteria.Currency },
        OperationDate = criteria.OperationDate is null
            ? null
            : new OperationDateFilterDto
            {
                FromDate = criteria.OperationDate.FromWireValue,
                ToDate = criteria.OperationDate.ToWireValue,
            },
        MoneyFlow = criteria.Flow is { } flow ? MoneyFlowParser.ToWireValue(flow) : null,
        Keyword = criteria.Keyword,
        PageSize = criteria.PageSize,
        PaginationKey = criteria.PaginationKey,
    };

    private async Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        _logger.BbvaResponded((int)response.StatusCode, body.Length);

        return body;
    }

    private T? Deserialize<T>(string body)
        where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, SerializerOptions);
        }
        catch (JsonException exception)
        {
            _logger.UnreadableBody(exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Turns a failed response into a domain error, preferring BBVA's own message so the
    /// caller sees exactly what the bank said rather than a paraphrase.
    /// </summary>
    private DomainError ToError(HttpStatusCode statusCode, string body)
    {
        var envelope = Deserialize<SearchResponseDto>(body);

        if (FindError(envelope?.Messages) is { } reported)
        {
            return reported;
        }

        return statusCode switch
        {
            HttpStatusCode.Unauthorized => DomainError.NotAuthorized(),
            HttpStatusCode.Forbidden => DomainError.AccessDenied(),
            HttpStatusCode.BadRequest => DomainError.Bad("Invalid parameters."),
            _ => new DomainError(
                DomainError.BadRequest,
                $"BBVA returned an unexpected HTTP {(int)statusCode} response."),
        };
    }

    private static DomainError? FindError(IReadOnlyList<MessageDto>? messages)
    {
        var error = messages?.FirstOrDefault(message =>
            string.Equals(message.Type, DomainError.ErrorType, StringComparison.OrdinalIgnoreCase));

        return error is null
            ? null
            : new DomainError(
                error.Code ?? DomainError.BadRequest,
                error.Message ?? "BBVA reported an error without a message.");
    }
}

internal static partial class BbvaClientLog
{
    [LoggerMessage(
        EventId = 1201,
        Level = LogLevel.Debug,
        Message = "BBVA responded with HTTP {StatusCode} ({BodyLength} bytes)")]
    public static partial void BbvaResponded(this ILogger logger, int statusCode, int bodyLength);

    [LoggerMessage(
        EventId = 1202,
        Level = LogLevel.Warning,
        Message = "Could not deserialize the BBVA response body: {Reason}")]
    public static partial void UnreadableBody(this ILogger logger, string reason);
}
