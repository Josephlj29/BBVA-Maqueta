using Bbva.Reconciliation.Api.Contracts;
using Bbva.Reconciliation.Application.Ports;
using Bbva.Reconciliation.Infrastructure.Auth;
using Bbva.Reconciliation.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Api.Endpoints;

/// <summary>Health and signing diagnostics.</summary>
public static class DiagnosticsEndpoints
{
    public static IEndpointRouteBuilder MapDiagnosticsEndpoints(this IEndpointRouteBuilder routes)
    {
        ArgumentNullException.ThrowIfNull(routes);

        var group = routes.MapGroup("/api/v1")
            .WithTags("Diagnostics");

        group.MapGet("/health", GetHealthAsync)
            .WithName("Health")
            .WithSummary("Service and BBVA connectivity health")
            .WithDescription(
                "Reports whether this service can obtain an OAuth token from BBVA. A failure " +
                "here means the credentials or the network path are wrong, before any business " +
                "call is attempted.")
            .Produces<HealthResponse>(StatusCodes.Status200OK)
            .Produces<HealthResponse>(StatusCodes.Status503ServiceUnavailable);

        group.MapPost("/diagnostics/signature-preview", PreviewSignature)
            .WithName("PreviewSignature")
            .WithSummary("Compute a request signature without sending it")
            .WithDescription(
                "Returns the digest, the exact string that would be signed, the Date header and " +
                "the resulting Signature. BBVA answers a bad signature with a bare 401 and no " +
                "diagnostics, so being able to inspect the signed string is the fastest way to " +
                "find a mismatch.")
            .Produces<SignaturePreview>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest);

        return routes;
    }

    private static async Task<IResult> GetHealthAsync(
        IBbvaTokenProvider tokenProvider,
        IOptions<BbvaOptions> options,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;

        try
        {
            var token = await tokenProvider.GetTokenAsync(cancellationToken);

            return Results.Ok(new HealthResponse(
                Status: "healthy",
                BaseUrl: settings.BaseUrl,
                TokenAcquired: true,
                TokenExpiresAt: token.ExpiresAt,
                TokenValidForSeconds: (int)Math.Max(0, (token.ExpiresAt - clock.GetUtcNow()).TotalSeconds),
                Detail: null));
        }
        catch (BbvaAuthenticationException exception)
        {
            return Results.Json(
                new HealthResponse("unhealthy", settings.BaseUrl, false, null, null, exception.Message),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (HttpRequestException exception)
        {
            return Results.Json(
                new HealthResponse("unhealthy", settings.BaseUrl, false, null, null, exception.Message),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IResult PreviewSignature(
        SignaturePreviewApiRequest request,
        ISignaturePreviewService previewService)
    {
        if (string.IsNullOrWhiteSpace(request.Method) || string.IsNullOrWhiteSpace(request.Path))
        {
            return Results.Problem(
                title: "Invalid parameters",
                detail: "Both method and path are required to compute a signature.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (!request.Path.StartsWith('/'))
        {
            return Results.Problem(
                title: "Invalid parameters",
                detail: "path must be an absolute path beginning with '/'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        return Results.Ok(previewService.Preview(request.Method, request.Path, request.Body));
    }
}

/// <param name="Status">Either <c>healthy</c> or <c>unhealthy</c>.</param>
/// <param name="BaseUrl">The BBVA API Market root this service is pointed at.</param>
/// <param name="TokenAcquired">Whether an OAuth token could be obtained.</param>
/// <param name="TokenExpiresAt">When the cached token stops being used.</param>
/// <param name="TokenValidForSeconds">Remaining lifetime of the cached token.</param>
/// <param name="Detail">Why the check failed, when it did.</param>
public sealed record HealthResponse(
    string Status,
    string BaseUrl,
    bool TokenAcquired,
    DateTimeOffset? TokenExpiresAt,
    int? TokenValidForSeconds,
    string? Detail);
