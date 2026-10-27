using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Bbva.Reconciliation.Api.OpenApi;

/// <summary>
/// Fills in the document-level metadata that makes the generated Scalar reference usable
/// as onboarding material rather than a bare list of routes.
/// </summary>
internal sealed class BbvaOpenApiDocumentTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        document.Info = new OpenApiInfo
        {
            Title = "BBVA Business Reconciliation",
            Version = "v1",
            Description = Description,
            Contact = new OpenApiContact
            {
                Name = "BBVA Open Banking — API Market",
                Url = new Uri("https://developer.bbva.com"),
            },
        };

        // Tags is an ISet in Microsoft.OpenApi 2.x, so it is populated rather than assigned.
        document.Tags ??= new HashSet<OpenApiTag>();
        document.Tags.Clear();

        document.Tags.Add(new OpenApiTag
        {
            Name = "Movements",
            Description = "The two business operations of BBVA's Business Reconciliation API v1.0.",
        });

        document.Tags.Add(new OpenApiTag
        {
            Name = "Sandbox certification",
            Description =
                "The official sandbox test plan, runnable scenario by scenario with a diff " +
                "against the documented response. Not registered in production.",
        });

        document.Tags.Add(new OpenApiTag
        {
            Name = "Diagnostics",
            Description = "Health checks and signature inspection.",
        });

        return Task.CompletedTask;
    }

    private const string Description = """
        A façade over BBVA Peru's Business Reconciliation API v1.0.

        **Why this service exists.** BBVA requires OAuth 2.0 *plus* an RSA-SHA512 message
        signature over every request. The signing key cannot live in a browser, so the
        browser talks to this service and this service talks to BBVA.

        **What it adds over calling BBVA directly**

        - Access tokens are fetched and cached automatically; there is no token to paste.
        - Every request is signed after the final path is known, so path parameters can
          never invalidate the signature.
        - Requests are validated locally against BBVA's own rules, using BBVA's own error
          wording, so an invalid search fails instantly instead of after a round trip.
        - BBVA's `messages` envelope is preserved in the `messages` extension of every
          problem response, so nothing the bank said is lost in translation.

        **Things worth knowing**

        - A search with no matches returns **200**, with `infoMessage` set to
          `"No transactions to show."` — an empty result, not an error.
        - `contracts` accepts exactly one 18-digit account number.
        - `transactionId` is exactly 31 alphanumeric characters.
        - Dates use `yyyy-MM-ddTHH:mm:ss.SSSZ`; `fromDate` cannot precede 2024-01-01 and
          `toDate` cannot be in the future. Supplying one requires the other.
        - Pagination is cursor based: pass a previous response's `pagination.next` back as
          `paginationKey`. The cursor is opaque — never construct one by hand.
        """;
}
