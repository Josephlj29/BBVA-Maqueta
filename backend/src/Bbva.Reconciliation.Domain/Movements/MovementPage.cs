namespace Bbva.Reconciliation.Domain.Movements;

/// <summary>
/// One page of search results. An empty page is a legitimate 200 response: the bank
/// answers <c>{"messages":[{"message":"No transactions to show.","type":"INFO"}]}</c>
/// rather than an error or an empty array.
/// </summary>
/// <param name="Movements">The movements on this page, in the order the bank returned them.</param>
/// <param name="Pagination">Cursor metadata, absent when there were no results.</param>
/// <param name="InfoMessage">The bank's INFO message when the result set is empty.</param>
public sealed record MovementPage(
    IReadOnlyList<Movement> Movements,
    PaginationInfo? Pagination,
    string? InfoMessage)
{
    public bool IsEmpty => Movements.Count == 0;

    public static MovementPage Empty(string? infoMessage = null) =>
        new([], null, infoMessage);
}

/// <param name="TotalPages">Total pages for the supplied filters.</param>
/// <param name="TotalElements">Total matching movements.</param>
/// <param name="PageSize">Items per page.</param>
/// <param name="Keys">Opaque cursors for navigation.</param>
public sealed record PaginationInfo(
    int? TotalPages,
    int? TotalElements,
    int? PageSize,
    PaginationKeys Keys);

/// <summary>
/// Opaque pagination cursors such as <c>L:-1|G:50|B:-1.0</c>. They are echoed back
/// verbatim in <c>paginationKey</c>; never parse or construct them by hand.
/// </summary>
public sealed record PaginationKeys(
    string? First,
    string? Last,
    string? Previous,
    string? Next)
{
    /// <summary>Whether a further page can be requested with <see cref="Next"/>.</summary>
    public bool HasNext => !string.IsNullOrWhiteSpace(Next);
}
