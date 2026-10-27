using System.Text.Json;
using Bbva.Reconciliation.Application.Sandbox;

namespace Bbva.Reconciliation.Application.Tests.Sandbox;

public sealed class JsonComparerTests
{
    [Fact]
    public void Identical_documents_have_no_differences()
    {
        var document = Parse("""{"a":1,"b":{"c":"x"}}""");

        JsonComparer.Compare(document, document).ShouldBeEmpty();
    }

    [Fact]
    public void Key_order_does_not_matter()
    {
        // BBVA does not guarantee property order; only structure and values matter.
        var expected = Parse("""{"a":1,"b":2}""");
        var actual = Parse("""{"b":2,"a":1}""");

        JsonComparer.Compare(expected, actual).ShouldBeEmpty();
    }

    [Fact]
    public void Reports_a_changed_scalar_with_its_path()
    {
        var expected = Parse("""{"postedBalance":{"amount":100.5}}""");
        var actual = Parse("""{"postedBalance":{"amount":99.0}}""");

        var differences = JsonComparer.Compare(expected, actual);

        differences.ShouldHaveSingleItem();
        differences[0].Path.ShouldBe("postedBalance.amount");
        differences[0].Expected.ShouldBe("100.5");
        differences[0].Actual.ShouldBe("99.0");
    }

    [Fact]
    public void Reports_a_missing_property()
    {
        var differences = JsonComparer.Compare(Parse("""{"a":1,"b":2}"""), Parse("""{"a":1}"""));

        differences.ShouldHaveSingleItem();
        differences[0].Path.ShouldBe("b");
        differences[0].Actual.ShouldBeNull();
    }

    [Fact]
    public void Reports_an_unexpected_property()
    {
        var differences = JsonComparer.Compare(Parse("""{"a":1}"""), Parse("""{"a":1,"extra":true}"""));

        differences.ShouldHaveSingleItem();
        differences[0].Path.ShouldBe("extra");
        differences[0].Expected.ShouldBeNull();
    }

    [Fact]
    public void Reports_an_array_length_mismatch()
    {
        var expected = Parse("""{"movements":[{"id":"a"},{"id":"b"}]}""");
        var actual = Parse("""{"movements":[{"id":"a"}]}""");

        var differences = JsonComparer.Compare(expected, actual);

        differences.ShouldContain(difference => difference.Path == "movements.length");
    }

    [Fact]
    public void Locates_a_difference_inside_an_array_element()
    {
        var expected = Parse("""{"movements":[{"id":"a"},{"id":"b"}]}""");
        var actual = Parse("""{"movements":[{"id":"a"},{"id":"CHANGED"}]}""");

        var differences = JsonComparer.Compare(expected, actual);

        differences.ShouldHaveSingleItem();
        differences[0].Path.ShouldBe("movements[1].id");
    }

    [Fact]
    public void Reports_a_type_mismatch()
    {
        var differences = JsonComparer.Compare(Parse("""{"a":1}"""), Parse("""{"a":"1"}"""));

        differences.ShouldHaveSingleItem();
        differences[0].Path.ShouldBe("a");
    }

    [Fact]
    public void Ignored_paths_are_skipped()
    {
        var expected = Parse("""{"postedBalance":{"amount":100.5},"id":"x"}""");
        var actual = Parse("""{"postedBalance":{"amount":7.0},"id":"x"}""");

        var ignored = new HashSet<string>(StringComparer.Ordinal) { "postedBalance.amount" };

        JsonComparer.Compare(expected, actual, ignored).ShouldBeEmpty();
    }

    [Fact]
    public void Ignored_paths_apply_to_every_array_element()
    {
        // Balances drift as the sandbox data set evolves; the shape still has to match.
        var expected = Parse("""{"movements":[{"postedBalance":{"amount":1}},{"postedBalance":{"amount":2}}]}""");
        var actual = Parse("""{"movements":[{"postedBalance":{"amount":9}},{"postedBalance":{"amount":8}}]}""");

        var ignored = new HashSet<string>(StringComparer.Ordinal) { "movements.postedBalance.amount" };

        JsonComparer.Compare(expected, actual, ignored).ShouldBeEmpty();
    }

    [Fact]
    public void A_missing_actual_document_is_a_single_root_difference()
    {
        var differences = JsonComparer.Compare(Parse("""{"a":1}"""), null);

        differences.ShouldHaveSingleItem();
        differences[0].Path.ShouldBe("$");
        differences[0].Actual.ShouldBeNull();
    }

    [Fact]
    public void Two_absent_documents_match()
    {
        JsonComparer.Compare(null, null).ShouldBeEmpty();
    }

    [Fact]
    public void Distinguishes_null_from_absent()
    {
        var differences = JsonComparer.Compare(Parse("""{"a":null}"""), Parse("""{}"""));

        differences.ShouldHaveSingleItem();
        differences[0].Expected.ShouldBe("null");
        differences[0].Actual.ShouldBeNull();
    }

    private static JsonElement Parse(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
