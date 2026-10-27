using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bbva.Reconciliation.Application.Sandbox;

/// <summary>
/// One scenario from the official BBVA sandbox test plan, extracted verbatim from the
/// workbook shipped with the technical documentation.
/// </summary>
/// <param name="Id">Stable identifier, e.g. <c>SRCH-03</c> or <c>DTL-11</c>.</param>
/// <param name="Group">Either <c>search</c> or <c>detail</c>.</param>
/// <param name="Name">Slug derived from the description.</param>
/// <param name="Description">The original Spanish description from the workbook.</param>
/// <param name="Method">HTTP method.</param>
/// <param name="Path">API path, with <c>{accountId}</c>/<c>{transactionId}</c> placeholders for detail cases.</param>
/// <param name="ExpectedStatusCode">Status code the bank is documented to return.</param>
/// <param name="RequestBody">Request body, absent for detail cases.</param>
/// <param name="ExpectedResponse">Full expected response body.</param>
/// <param name="PathParameters">Values for the path placeholders, detail cases only.</param>
/// <param name="FollowUp">Additional calls that continue the scenario, such as fetching the next page.</param>
public sealed record SandboxTestCase(
    string Id,
    string Group,
    string Name,
    string Description,
    string Method,
    string Path,
    int ExpectedStatusCode,
    JsonElement? RequestBody,
    JsonElement? ExpectedResponse,
    IReadOnlyDictionary<string, string>? PathParameters = null,
    IReadOnlyList<SandboxFollowUp>? FollowUp = null)
{
    /// <summary>Resolves the path placeholders using <see cref="PathParameters"/>.</summary>
    public string ResolvedPath()
    {
        if (PathParameters is null || PathParameters.Count == 0)
        {
            return Path;
        }

        var resolved = Path;

        foreach (var (key, value) in PathParameters)
        {
            resolved = resolved.Replace($"{{{key}}}", value, StringComparison.Ordinal);
        }

        return resolved;
    }

    /// <summary>Whether the scenario expects a failure rather than a successful response.</summary>
    public bool ExpectsError => ExpectedStatusCode >= 400;
}

/// <param name="Step">1-based position of this call within the scenario, starting at 2.</param>
/// <param name="RequestBody">Body of the follow-up call.</param>
/// <param name="ExpectedStatusCode">Expected status code.</param>
/// <param name="ExpectedResponse">Expected response body.</param>
public sealed record SandboxFollowUp(
    int Step,
    JsonElement? RequestBody,
    int ExpectedStatusCode,
    JsonElement? ExpectedResponse);

/// <summary>The full test plan as loaded from the fixture.</summary>
/// <param name="Source">Name of the workbook the cases came from.</param>
/// <param name="Api">API name and version.</param>
/// <param name="SandboxBaseUrl">Host the cases are meant to run against.</param>
/// <param name="TotalCases">Number of scenarios.</param>
/// <param name="TotalRequests">Number of HTTP calls, counting follow-ups.</param>
/// <param name="Cases">The scenarios, in workbook order.</param>
public sealed record SandboxTestPlan(
    string Source,
    string Api,
    string SandboxBaseUrl,
    int TotalCases,
    int TotalRequests,
    IReadOnlyList<SandboxTestCase> Cases)
{
    [JsonIgnore]
    public IEnumerable<SandboxTestCase> SearchCases =>
        Cases.Where(testCase => testCase.Group == "search");

    [JsonIgnore]
    public IEnumerable<SandboxTestCase> DetailCases =>
        Cases.Where(testCase => testCase.Group == "detail");

    public SandboxTestCase? Find(string id) =>
        Cases.FirstOrDefault(testCase =>
            string.Equals(testCase.Id, id, StringComparison.OrdinalIgnoreCase));
}
