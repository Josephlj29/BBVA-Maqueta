using Bbva.Reconciliation.Domain.Errors;
using Microsoft.AspNetCore.Mvc;

namespace Bbva.Reconciliation.Api.Errors;

/// <summary>
/// Translates a <see cref="DomainError"/> into an HTTP result.
/// </summary>
/// <remarks>
/// The response is RFC 9457 problem details — the shape a .NET or Angular client expects —
/// but BBVA's original <c>messages</c> envelope is carried along in an extension member.
/// Callers integrating against the bank need the exact code and wording BBVA produced;
/// paraphrasing it would make their own error handling guesswork.
/// </remarks>
public static class DomainErrorResults
{
    public static IResult ToResult(this DomainError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var status = ToStatusCode(error.Code);

        var problem = new ProblemDetails
        {
            Status = status,
            Title = ToTitle(error.Code),
            Detail = error.Message,
            Type = $"https://developer.bbva.com/errors/{error.Code}",
        };

        // The bank's own envelope, verbatim, so a client can react to code and message
        // without depending on this API's translation of them.
        problem.Extensions["messages"] = new[]
        {
            new { code = error.Code, message = error.Message, type = error.Type },
        };

        return Results.Problem(problem);
    }

    private static int ToStatusCode(string code) => code switch
    {
        DomainError.Unauthorized => StatusCodes.Status401Unauthorized,
        DomainError.Forbidden => StatusCodes.Status403Forbidden,
        _ => StatusCodes.Status400BadRequest,
    };

    private static string ToTitle(string code) => code switch
    {
        DomainError.Unauthorized => "Unauthorized",
        DomainError.Forbidden => "Access denied",
        DomainError.InvalidParameters => "Invalid parameters",
        _ => "Bad request",
    };
}
