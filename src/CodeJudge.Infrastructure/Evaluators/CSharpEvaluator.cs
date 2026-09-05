using System.Diagnostics;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using CodeJudge.Application.Abstractions;
using CodeJudge.Application.Evaluation;
using CodeJudge.Application.Problems;
using CodeJudge.Domain.Enums;
using CodeJudge.Infrastructure.Workers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Options;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>
/// C# evaluator (System Design §6.5). Compiles the submission with Roslyn — returning compiler
/// diagnostics on failure — then loads the emitted assembly into a collectible
/// <see cref="AssemblyLoadContext"/> and invokes the problem's function per test case by reflection,
/// enforcing the run timeout with <c>Task.WaitAsync</c>.
/// <para>
/// A submission may be a bare method (as the catalog signature suggests) or a complete type; a bare
/// method is wrapped in a generated <c>Solution</c> class. Execution is in-process, so a runaway
/// thread cannot be killed — the documented v1 limitation (System Design §11).
/// </para>
/// </summary>
public sealed class CSharpEvaluator : ICodeEvaluator
{
    private const string WrapperClassName = "Solution";

    private static readonly string[] ReferencedAssemblies =
    [
        "System.Private.CoreLib",
        "System.Runtime",
        "System.Runtime.Extensions",
        "System.Collections",
        "System.Linq",
        "System.Console",
        "System.Text.RegularExpressions",
    ];

    private static readonly IReadOnlyList<MetadataReference> References = BuildReferences();

    private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);

    private static readonly CSharpCompilationOptions CompilationOptions = new(
        OutputKind.DynamicallyLinkedLibrary,
        optimizationLevel: OptimizationLevel.Release,
        allowUnsafe: false,
        nullableContextOptions: NullableContextOptions.Disable);

    // Implicit usings so a bare-method submission does not have to spell them out.
    private static readonly SyntaxTree GlobalUsings = CSharpSyntaxTree.ParseText(
        "global using System;\n"
        + "global using System.Collections.Generic;\n"
        + "global using System.Linq;\n"
        + "global using System.Text;\n",
        ParseOptions,
        path: "GlobalUsings.cs");

    private readonly EvaluationSettings _settings;

    public CSharpEvaluator(IOptions<EvaluationSettings> settings)
    {
        _settings = settings.Value;
    }

    public Language Language => Language.CSharp;

    public Task<CompileResult> CompileAsync(string code, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var program = Prepare(code, cancellationToken);
        var compilation = CreateCompilation(program, cancellationToken);

        var errors = compilation.GetDiagnostics(cancellationToken)
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => ToDiagnostic(d, program.LineOffset))
            .ToList();

        stopwatch.Stop();
        var durationMs = (int)stopwatch.ElapsedMilliseconds;

        return Task.FromResult(errors.Count == 0
            ? CompileResult.Ok(durationMs)
            : CompileResult.Failure(errors, durationMs));
    }

    public async Task<HarnessRunResult> RunAsync(
        string code,
        ProblemDefinition problem,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var program = Prepare(code, cancellationToken);
        var compilation = CreateCompilation(program, cancellationToken);

        using var stream = new MemoryStream();
        var emit = compilation.Emit(stream, cancellationToken: cancellationToken);
        if (!emit.Success)
        {
            var first = emit.Diagnostics.FirstOrDefault(d => d.Severity == DiagnosticSeverity.Error);
            return Crash($"Compilation failed: {first?.GetMessage() ?? "unknown error"}", stopwatch);
        }

        stream.Position = 0;
        var context = new SubmissionLoadContext();
        try
        {
            var assembly = context.LoadFromStream(stream);
            var functionName = problem.Signatures[Language].FunctionName;

            var method = FindMethod(assembly, functionName);
            if (method is null)
            {
                return Crash($"No method named '{functionName}' was found in the submission.", stopwatch);
            }

            object? instance = null;
            if (!method.IsStatic)
            {
                try
                {
                    instance = Activator.CreateInstance(method.DeclaringType!, nonPublic: true);
                }
                catch (Exception ex) when (ex is MissingMethodException or TargetInvocationException or MemberAccessException)
                {
                    return Crash(
                        $"Could not instantiate '{method.DeclaringType!.Name}': it needs a parameterless constructor.",
                        stopwatch);
                }
            }

            return await RunCasesAsync(method, instance, problem, stopwatch, cancellationToken);
        }
        finally
        {
            // Best-effort: the context only unloads once nothing references the assembly. A case
            // that timed out is still running on its abandoned thread and keeps it alive.
            context.Unload();
        }
    }

    private async Task<HarnessRunResult> RunCasesAsync(
        MethodInfo method,
        object? instance,
        ProblemDefinition problem,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        var budget = TimeSpan.FromSeconds(_settings.RunTimeoutSeconds);
        var parameters = method.GetParameters();
        var cases = new List<TestCaseResult>(problem.Cases.Count);

        foreach (var testCase in problem.Cases)
        {
            var remaining = budget - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
            {
                return TimedOut(cases, stopwatch);
            }

            object?[] args;
            try
            {
                args = BindArguments(parameters, testCase.Args);
            }
            catch (Exception ex) when (ex is JsonException or ArgumentException or NotSupportedException)
            {
                cases.Add(Errored(testCase, $"ArgumentBinding: {ex.Message}", 0));
                continue;
            }

            var caseStopwatch = Stopwatch.StartNew();
            object? returned;
            try
            {
                // Untrusted code runs on a pool thread so the worker can abandon it on timeout.
                returned = await Task.Run(() => method.Invoke(instance, args), CancellationToken.None)
                    .WaitAsync(remaining, cancellationToken);
            }
            catch (TimeoutException)
            {
                return TimedOut(cases, stopwatch);
            }
            catch (TargetInvocationException ex)
            {
                var inner = ex.InnerException ?? ex;
                cases.Add(Errored(testCase, $"{inner.GetType().Name}: {inner.Message}", caseStopwatch.Elapsed.TotalMilliseconds));
                continue;
            }

            caseStopwatch.Stop();

            JsonElement actual;
            try
            {
                actual = returned is null
                    ? JsonSerializer.SerializeToElement<object?>(null)
                    : JsonSerializer.SerializeToElement(returned, returned.GetType());
            }
            catch (NotSupportedException ex)
            {
                cases.Add(Errored(testCase, $"ResultSerialization: {ex.Message}", caseStopwatch.Elapsed.TotalMilliseconds));
                continue;
            }

            cases.Add(new TestCaseResult(
                testCase.Id,
                testCase.Expected,
                actual,
                JsonValueComparer.DeepEquals(actual, testCase.Expected),
                Error: null,
                caseStopwatch.Elapsed.TotalMilliseconds));
        }

        stopwatch.Stop();
        return new HarnessRunResult(cases, TimedOut: false, Stderr: null, (int)stopwatch.ElapsedMilliseconds);
    }

    // ---- Source preparation -------------------------------------------------------------------

    private sealed record PreparedProgram(string Source, int LineOffset);

    /// <summary>
    /// Returns the source to compile. Code that declares a type or namespace is used verbatim;
    /// anything else is treated as method(s) and wrapped in a <c>Solution</c> class. Using
    /// directives in wrapped code are hoisted onto the wrapper's single header line and blanked in
    /// place, so every user line keeps its column and moves down by exactly one line.
    /// </summary>
    private static PreparedProgram Prepare(string code, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(code, ParseOptions, cancellationToken: cancellationToken);
        var root = tree.GetCompilationUnitRoot(cancellationToken);

        var declaresType = root.Members.Any(m => m is BaseTypeDeclarationSyntax or BaseNamespaceDeclarationSyntax);
        if (declaresType)
        {
            return new PreparedProgram(code, 0);
        }

        var body = code.ToCharArray();
        var hoisted = new List<string>();
        foreach (var usingDirective in root.Usings)
        {
            hoisted.Add(usingDirective.ToString());
            var span = usingDirective.Span;
            for (var i = span.Start; i < span.End; i++)
            {
                if (body[i] is not ('\r' or '\n'))
                {
                    body[i] = ' ';
                }
            }
        }

        var header = string.Join(" ", hoisted) + (hoisted.Count > 0 ? " " : string.Empty)
                     + $"public class {WrapperClassName} {{";

        return new PreparedProgram(header + "\n" + new string(body) + "\n}\n", LineOffset: 1);
    }

    private static CSharpCompilation CreateCompilation(PreparedProgram program, CancellationToken cancellationToken)
    {
        var tree = CSharpSyntaxTree.ParseText(program.Source, ParseOptions, path: "Submission.cs", cancellationToken: cancellationToken);

        return CSharpCompilation.Create(
            $"CodeJudge.Submission_{Guid.CreateVersion7():N}",
            [GlobalUsings, tree],
            References,
            CompilationOptions);
    }

    private static CompileDiagnostic ToDiagnostic(Diagnostic diagnostic, int lineOffset)
    {
        var position = diagnostic.Location.GetLineSpan().StartLinePosition;
        var line = diagnostic.Location.IsInSource ? Math.Max(1, position.Line + 1 - lineOffset) : 0;
        var column = diagnostic.Location.IsInSource ? position.Character + 1 : 0;

        return new CompileDiagnostic(line, column, diagnostic.Id, diagnostic.GetMessage());
    }

    private static IReadOnlyList<MetadataReference> BuildReferences()
    {
        var trusted = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty)
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);

        // Test hosts can list the same assembly twice; the first path wins.
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var path in trusted)
        {
            byName.TryAdd(Path.GetFileNameWithoutExtension(path), path);
        }

        return ReferencedAssemblies
            .Where(byName.ContainsKey)
            .Select(name => (MetadataReference)MetadataReference.CreateFromFile(byName[name]))
            .ToList();
    }

    // ---- Reflection ----------------------------------------------------------------------------

    private static MethodInfo? FindMethod(Assembly assembly, string functionName)
    {
        const BindingFlags flags =
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly;

        var candidates = assembly.GetTypes()
            .Where(t => !t.Name.StartsWith('<'))
            .SelectMany(t => t.GetMethods(flags))
            .Where(m => string.Equals(m.Name, functionName, StringComparison.Ordinal) && !m.IsGenericMethodDefinition)
            .ToList();

        // Prefer the user-facing shape when overloaded: public, then static.
        return candidates
            .OrderByDescending(m => m.IsPublic)
            .ThenByDescending(m => m.IsStatic)
            .FirstOrDefault();
    }

    private static object?[] BindArguments(ParameterInfo[] parameters, JsonElement[] args)
    {
        if (parameters.Length != args.Length)
        {
            throw new ArgumentException(
                $"Function expects {parameters.Length} parameter(s) but the test case supplies {args.Length}.");
        }

        var bound = new object?[parameters.Length];
        for (var i = 0; i < parameters.Length; i++)
        {
            bound[i] = JsonSerializer.Deserialize(args[i], parameters[i].ParameterType);
        }

        return bound;
    }

    // ---- Result helpers ------------------------------------------------------------------------

    private static TestCaseResult Errored(TestCase testCase, string error, double durationMs)
        => new(testCase.Id, testCase.Expected, Actual: null, Passed: false, error, durationMs);

    private static HarnessRunResult TimedOut(List<TestCaseResult> cases, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new HarnessRunResult(cases, TimedOut: true, Stderr: null, (int)stopwatch.ElapsedMilliseconds);
    }

    private static HarnessRunResult Crash(string message, Stopwatch stopwatch)
    {
        stopwatch.Stop();
        return new HarnessRunResult([], TimedOut: false, Stderr: message, (int)stopwatch.ElapsedMilliseconds);
    }

    /// <summary>Collectible context so each submission's assembly can be unloaded after the run.</summary>
    private sealed class SubmissionLoadContext : AssemblyLoadContext
    {
        public SubmissionLoadContext()
            : base(name: "CodeJudge.Submission", isCollectible: true)
        {
        }

        // Resolve everything from the default context; submissions carry no dependencies of their own.
        protected override Assembly? Load(AssemblyName assemblyName) => null;
    }
}
