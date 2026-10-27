namespace Bbva.Reconciliation.Domain.ValueObjects;

/// <summary>
/// The eleven movement states listed in the BBVA technical documentation.
/// </summary>
public enum TransactionStatus
{
    /// <summary>The transaction has been fulfilled.</summary>
    Settled,

    /// <summary>The transaction has not been fulfilled yet.</summary>
    Pending,

    /// <summary>The transaction has been sent.</summary>
    Sent,

    /// <summary>The transaction has been returned.</summary>
    Returned,

    /// <summary>The transaction has been rejected.</summary>
    Rejected,

    /// <summary>The transaction has not been authorised yet.</summary>
    AuthorizationPending,

    /// <summary>The transaction has been cancelled.</summary>
    Canceled,

    /// <summary>The transaction has been refused.</summary>
    Refused,

    /// <summary>The transaction has been refunded.</summary>
    Refund,

    /// <summary>The transaction has been charged back.</summary>
    Chargebacked,

    /// <summary>The transaction has been charged.</summary>
    Charged,
}

public static class TransactionStatusParser
{
    private static readonly Dictionary<string, TransactionStatus> ByWireValue = new(StringComparer.Ordinal)
    {
        ["SETTLED"] = TransactionStatus.Settled,
        ["PENDING"] = TransactionStatus.Pending,
        ["SENT"] = TransactionStatus.Sent,
        ["RETURNED"] = TransactionStatus.Returned,
        ["REJECTED"] = TransactionStatus.Rejected,
        ["AUTHORIZATION_PENDING"] = TransactionStatus.AuthorizationPending,
        ["CANCELED"] = TransactionStatus.Canceled,
        ["REFUSED"] = TransactionStatus.Refused,
        ["REFUND"] = TransactionStatus.Refund,
        ["CHARGEBACKED"] = TransactionStatus.Chargebacked,
        ["CHARGED"] = TransactionStatus.Charged,
    };

    private static readonly Dictionary<TransactionStatus, string> ToWire =
        ByWireValue.ToDictionary(pair => pair.Value, pair => pair.Key);

    /// <summary>
    /// Returns <c>null</c> for anything unrecognised rather than throwing: the bank may
    /// add a state before this client knows about it, and one unknown status must not
    /// fail a whole page of movements.
    /// </summary>
    public static TransactionStatus? Parse(string? raw) =>
        raw is not null && ByWireValue.TryGetValue(raw, out var status) ? status : null;

    public static string ToWireValue(TransactionStatus status) =>
        ToWire.TryGetValue(status, out var raw)
            ? raw
            : throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown transaction status.");
}
