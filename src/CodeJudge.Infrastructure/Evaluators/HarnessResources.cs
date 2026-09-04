using System.Reflection;

namespace CodeJudge.Infrastructure.Evaluators;

/// <summary>Loads the embedded language harness scripts (System Design §6.5).</summary>
internal static class HarnessResources
{
    public static string Load(string fileName)
    {
        var assembly = typeof(HarnessResources).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .Single(name => name.EndsWith(fileName, StringComparison.Ordinal));

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
