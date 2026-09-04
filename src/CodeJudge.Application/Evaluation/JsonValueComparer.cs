using System.Text.Json;

namespace CodeJudge.Application.Evaluation;

/// <summary>
/// Deep, normalised equality for JSON values, so that a harness's actual output can be compared
/// against a catalog's expected value regardless of language quirks — e.g. <c>[0,1]</c> from Python
/// and <c>[0, 1]</c> from JavaScript compare equal, and <c>7</c> equals <c>7.0</c>
/// (System Design §6.5).
/// </summary>
public static class JsonValueComparer
{
    public static bool DeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            // Treat the two boolean kinds as one logical kind.
            var aBool = a.ValueKind is JsonValueKind.True or JsonValueKind.False;
            var bBool = b.ValueKind is JsonValueKind.True or JsonValueKind.False;
            if (aBool && bBool)
            {
                return a.GetBoolean() == b.GetBoolean();
            }

            return false;
        }

        switch (a.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined:
                return true;

            case JsonValueKind.True:
            case JsonValueKind.False:
                return a.GetBoolean() == b.GetBoolean();

            case JsonValueKind.String:
                return string.Equals(a.GetString(), b.GetString(), StringComparison.Ordinal);

            case JsonValueKind.Number:
                return NumbersEqual(a, b);

            case JsonValueKind.Array:
                return ArraysEqual(a, b);

            case JsonValueKind.Object:
                return ObjectsEqual(a, b);

            default:
                return false;
        }
    }

    private static bool NumbersEqual(JsonElement a, JsonElement b)
    {
        // Prefer exact decimal comparison; fall back to double for values outside decimal range.
        if (a.TryGetDecimal(out var da) && b.TryGetDecimal(out var db))
        {
            return da == db;
        }

        return a.GetDouble() == b.GetDouble();
    }

    private static bool ArraysEqual(JsonElement a, JsonElement b)
    {
        if (a.GetArrayLength() != b.GetArrayLength())
        {
            return false;
        }

        using var ea = a.EnumerateArray();
        using var eb = b.EnumerateArray();
        while (ea.MoveNext() && eb.MoveNext())
        {
            if (!DeepEquals(ea.Current, eb.Current))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ObjectsEqual(JsonElement a, JsonElement b)
    {
        var countA = 0;
        foreach (var prop in a.EnumerateObject())
        {
            countA++;
            if (!b.TryGetProperty(prop.Name, out var other) || !DeepEquals(prop.Value, other))
            {
                return false;
            }
        }

        var countB = 0;
        foreach (var _ in b.EnumerateObject())
        {
            countB++;
        }

        return countA == countB;
    }
}
