using System.Text.Json;
using Bbva.Reconciliation.Application.Sandbox;
using Microsoft.Extensions.Options;

namespace Bbva.Reconciliation.Infrastructure.Sandbox;

/// <summary>Where to find the extracted sandbox test plan.</summary>
public sealed class SandboxOptions
{
    public const string SectionName = "Sandbox";

    /// <summary>
    /// Path to the fixture generated from the official workbook. Relative paths resolve
    /// against the application's content root.
    /// </summary>
    public string TestPlanPath { get; set; } = "fixtures/sandbox-test-plan.json";
}

/// <summary>
/// Loads the sandbox test plan from the JSON fixture extracted from the official workbook.
/// </summary>
/// <remarks>
/// The plan is read once and cached: it is a build-time artifact that never changes at
/// runtime, and it is the single source shared by the API, the integration tests and the
/// generated Bruno and Postman collections.
/// </remarks>
public sealed class JsonFileSandboxTestPlanRepository : ISandboxTestPlanRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly Lazy<SandboxTestPlan> _plan;

    public JsonFileSandboxTestPlanRepository(IOptions<SandboxOptions> options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var configured = options.Value.TestPlanPath;

        var path = Path.IsPathRooted(configured)
            ? configured
            : Path.GetFullPath(Path.Combine(contentRootPath, configured));

        _plan = new Lazy<SandboxTestPlan>(() => Load(path), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public SandboxTestPlan GetPlan() => _plan.Value;

    private static SandboxTestPlan Load(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                $"Sandbox test plan not found at '{path}'. Regenerate it from the official " +
                "workbook, or point Sandbox:TestPlanPath at the fixture.",
                path);
        }

        using var stream = File.OpenRead(path);

        return JsonSerializer.Deserialize<SandboxTestPlan>(stream, SerializerOptions)
            ?? throw new InvalidOperationException($"Sandbox test plan at '{path}' is empty or unreadable.");
    }
}
