namespace Bbva.Reconciliation.Domain.Errors;

/// <summary>
/// A validation or business failure expressed in the same shape BBVA uses on the wire:
/// <c>{"messages":[{"code","message","type"}]}</c>.
/// </summary>
/// <param name="Code">BBVA error code, e.g. <c>invalidParameters</c>, <c>badRequest</c>, <c>forbidden</c>.</param>
/// <param name="Message">Human readable message, kept verbatim so it matches the bank's own wording.</param>
/// <param name="Type">Severity marker. BBVA uses <c>ERROR</c> for failures and <c>INFO</c> for empty results.</param>
public sealed record DomainError(string Code, string Message, string Type = DomainError.ErrorType)
{
    public const string ErrorType = "ERROR";
    public const string InfoType = "INFO";

    public const string InvalidParameters = "invalidParameters";
    public const string BadRequest = "badRequest";
    public const string Unauthorized = "unauthorized";
    public const string Forbidden = "forbidden";

    public static DomainError Invalid(string message) => new(InvalidParameters, message);

    public static DomainError Bad(string message) => new(BadRequest, message);

    public static DomainError AccessDenied() => new(Forbidden, "Access denied");

    public static DomainError NotAuthorized() => new(Unauthorized, "Unauthorized");
}
