using System.Text.Json;
using CodeJudge.Application.Abstractions;
using CodeJudge.Domain.Enums;

namespace CodeJudge.Application.Problems;

/// <summary>
/// The v1 in-code problem catalog (System Design §6.5). Four problems, each with a per-language
/// signature and multiple test cases including edge cases. Arguments and expected values are
/// stored as language-neutral JSON. Registered as a singleton.
/// </summary>
public sealed class ProblemCatalog : IProblemCatalog
{
    private readonly IReadOnlyList<ProblemDefinition> _problems;
    private readonly Dictionary<string, ProblemDefinition> _byId;

    public ProblemCatalog()
    {
        _problems = BuildProblems();
        _byId = _problems.ToDictionary(p => p.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<ProblemDefinition> GetAll() => _problems;

    public ProblemDefinition? Find(string id) =>
        id is not null && _byId.TryGetValue(id, out var problem) ? problem : null;

    public bool Exists(string id) => id is not null && _byId.ContainsKey(id);

    // Serialises a CLR value to a self-contained JsonElement (safe to store long-term).
    private static JsonElement J(object? value) => JsonSerializer.SerializeToElement(value);

    private static ProblemDefinition[] BuildProblems() =>
    [
        new ProblemDefinition(
            Id: "sum-two-numbers",
            Title: "Sum Two Numbers",
            Description: "Return the sum of two integers a and b.",
            Difficulty: "Easy",
            Signatures: new Dictionary<Language, LanguageSignature>
            {
                [Language.CSharp] = new("Sum", "int Sum(int a, int b)"),
                [Language.Python] = new("sum_two", "def sum_two(a, b)"),
                [Language.JavaScript] = new("sumTwo", "function sumTwo(a, b)"),
            },
            Cases:
            [
                new TestCase(1, [J(3), J(4)], J(7), IsSample: true),
                new TestCase(2, [J(-2), J(2)], J(0), IsSample: false),
                new TestCase(3, [J(0), J(0)], J(0), IsSample: false),
                new TestCase(4, [J(1000000), J(1000000)], J(2000000), IsSample: false),
            ]),

        new ProblemDefinition(
            Id: "reverse-words",
            Title: "Reverse Words",
            Description: "Reverse the order of words in a string. Words are separated by one or more spaces; leading and trailing spaces are removed and words are joined by a single space.",
            Difficulty: "Easy",
            Signatures: new Dictionary<Language, LanguageSignature>
            {
                [Language.CSharp] = new("ReverseWords", "string ReverseWords(string s)"),
                [Language.Python] = new("reverse_words", "def reverse_words(s)"),
                [Language.JavaScript] = new("reverseWords", "function reverseWords(s)"),
            },
            Cases:
            [
                new TestCase(1, [J("the sky is blue")], J("blue is sky the"), IsSample: true),
                new TestCase(2, [J("hello")], J("hello"), IsSample: false),
                new TestCase(3, [J("  hello   world  ")], J("world hello"), IsSample: false),
                new TestCase(4, [J("")], J(""), IsSample: false),
            ]),

        new ProblemDefinition(
            Id: "balanced-brackets",
            Title: "Balanced Brackets",
            Description: "Return true if every opening bracket in the string has a matching closing bracket of the same type in the correct order. Brackets are (), [] and {}.",
            Difficulty: "Medium",
            Signatures: new Dictionary<Language, LanguageSignature>
            {
                [Language.CSharp] = new("IsBalanced", "bool IsBalanced(string s)"),
                [Language.Python] = new("is_balanced", "def is_balanced(s)"),
                [Language.JavaScript] = new("isBalanced", "function isBalanced(s)"),
            },
            Cases:
            [
                new TestCase(1, [J("([]{})")], J(true), IsSample: true),
                new TestCase(2, [J("([)]")], J(false), IsSample: false),
                new TestCase(3, [J("")], J(true), IsSample: false),
                new TestCase(4, [J("(((")], J(false), IsSample: false),
                new TestCase(5, [J(")")], J(false), IsSample: false),
            ]),

        new ProblemDefinition(
            Id: "two-sum",
            Title: "Two Sum",
            Description: "Given an array of integers and a target, return the indices of the two numbers that add up to the target. Exactly one solution exists and an element may not be reused.",
            Difficulty: "Easy",
            Signatures: new Dictionary<Language, LanguageSignature>
            {
                [Language.CSharp] = new("TwoSum", "int[] TwoSum(int[] nums, int target)"),
                [Language.Python] = new("two_sum", "def two_sum(nums, target)"),
                [Language.JavaScript] = new("twoSum", "function twoSum(nums, target)"),
            },
            Cases:
            [
                new TestCase(1, [J(new[] { 2, 7, 11, 15 }), J(9)], J(new[] { 0, 1 }), IsSample: true),
                new TestCase(2, [J(new[] { 3, 2, 4 }), J(6)], J(new[] { 1, 2 }), IsSample: false),
                new TestCase(3, [J(new[] { 3, 3 }), J(6)], J(new[] { 0, 1 }), IsSample: false),
                new TestCase(4, [J(new[] { -1, -2, -3, -4, -5 }), J(-8)], J(new[] { 2, 4 }), IsSample: false),
            ]),
    ];
}
