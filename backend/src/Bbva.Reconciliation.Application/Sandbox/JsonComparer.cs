using System.Text.Json;

namespace Bbva.Reconciliation.Application.Sandbox;

/// <summary>One difference between an expected and an actual JSON document.</summary>
/// <param name="Path">JSON pointer style location, e.g. <c>movements[0].postedBalance.amount</c>.</param>
/// <param name="Expected">Rendered expected value, or <c>null</c> when the node was absent.</param>
/// <param name="Actual">Rendered actual value, or <c>null</c> when the node was absent.</param>
public sealed record JsonDifference(string Path, string? Expected, string? Actual);

/// <summary>
/// Structural JSON comparison for the sandbox runner.
/// </summary>
/// <remarks>
/// Textual comparison is useless here: the bank reorders object keys, and the workbook's
/// captured balances move as the sandbox data set evolves. This walks both documents and
/// reports located differences so a failure says which field drifted, not merely that two
/// strings differ.
/// </remarks>
public static class JsonComparer
{
    /// <summary>Maximum differences collected before giving up, to keep reports readable.</summary>
    private const int MaxDifferences = 50;

    /// <summary>
    /// Compares two documents and returns every difference found.
    /// </summary>
    /// <param name="expected">The workbook's documented response.</param>
    /// <param name="actual">What the sandbox actually returned.</param>
    /// <param name="ignoredPaths">
    /// Paths excluded from comparison. Balances and dates legitimately move between runs,
    /// so a caller certifying request/response shape can exclude them without ignoring the
    /// structural check as a whole.
    /// </param>
    public static IReadOnlyList<JsonDifference> Compare(
        JsonElement? expected,
        JsonElement? actual,
        IReadOnlySet<string>? ignoredPaths = null)
    {
        var differences = new List<JsonDifference>();

        if (expected is null && actual is null)
        {
            return differences;
        }

        if (expected is null || actual is null)
        {
            differences.Add(new JsonDifference(
                "$",
                expected is null ? null : Render(expected.Value),
                actual is null ? null : Render(actual.Value)));

            return differences;
        }

        Walk("$", expected.Value, actual.Value, differences, ignoredPaths ?? new HashSet<string>(StringComparer.Ordinal));

        return differences;
    }

    private static void Walk(
        string path,
        JsonElement expected,
        JsonElement actual,
        List<JsonDifference> differences,
        IReadOnlySet<string> ignoredPaths)
    {
        if (differences.Count >= MaxDifferences || IsIgnored(path, ignoredPaths))
        {
            return;
        }

        if (expected.ValueKind != actual.ValueKind)
        {
            differences.Add(new JsonDifference(path, Render(expected), Render(actual)));
            return;
        }

        switch (expected.ValueKind)
        {
            case JsonValueKind.Object:
                WalkObject(path, expected, actual, differences, ignoredPaths);
                break;

            case JsonValueKind.Array:
                WalkArray(path, expected, actual, differences, ignoredPaths);
                break;

            default:
                if (!Render(expected).Equals(Render(actual), StringComparison.Ordinal))
                {
                    differences.Add(new JsonDifference(path, Render(expected), Render(actual)));
                }

                break;
        }
    }

    private static void WalkObject(
        string path,
        JsonElement expected,
        JsonElement actual,
        List<JsonDifference> differences,
        IReadOnlySet<string> ignoredPaths)
    {
        foreach (var property in expected.EnumerateObject())
        {
            var childPath = path == "$" ? property.Name : $"{path}.{property.Name}";

            if (actual.TryGetProperty(property.Name, out var actualValue))
            {
                Walk(childPath, property.Value, actualValue, differences, ignoredPaths);
            }
            else if (!IsIgnored(childPath, ignoredPaths))
            {
                differences.Add(new JsonDifference(childPath, Render(property.Value), null));
            }
        }

        foreach (var property in actual.EnumerateObject())
        {
            var childPath = path == "$" ? property.Name : $"{path}.{property.Name}";

            if (!expected.TryGetProperty(property.Name, out _) && !IsIgnored(childPath, ignoredPaths))
            {
                differences.Add(new JsonDifference(childPath, null, Render(property.Value)));
            }
        }
    }

    private static void WalkArray(
        string path,
        JsonElement expected,
        JsonElement actual,
        List<JsonDifference> differences,
        IReadOnlySet<string> ignoredPaths)
    {
        var expectedItems = expected.EnumerateArray().ToList();
        var actualItems = actual.EnumerateArray().ToList();

        if (expectedItems.Count != actualItems.Count)
        {
            differences.Add(new JsonDifference(
                $"{path}.length",
                expectedItems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                actualItems.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var shared = Math.Min(expectedItems.Count, actualItems.Count);

        for (var index = 0; index < shared; index++)
        {
            Walk($"{path}[{index}]", expectedItems[index], actualItems[index], differences, ignoredPaths);
        }
    }

    /// <summary>
    /// Matches an ignore entry against a path, treating array indices as wildcards so
    /// <c>movements.postedBalance.amount</c> covers every element of the array.
    /// </summary>
    private static bool IsIgnored(string path, IReadOnlySet<string> ignoredPaths)
    {
        if (ignoredPaths.Count == 0)
        {
            return false;
        }

        if (ignoredPaths.Contains(path))
        {
            return true;
        }

        var normalized = StripIndices(path);

        return ignoredPaths.Contains(normalized);
    }

    private static string StripIndices(string path)
    {
        var builder = new System.Text.StringBuilder(path.Length);
        var skipping = false;

        foreach (var character in path)
        {
            if (character == '[')
            {
                skipping = true;
            }
            else if (character == ']')
            {
                skipping = false;
            }
            else if (!skipping)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string Render(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.String => element.GetString() ?? string.Empty,
        JsonValueKind.Null => "null",
        JsonValueKind.Undefined => "undefined",
        _ => element.GetRawText(),
    };
}
