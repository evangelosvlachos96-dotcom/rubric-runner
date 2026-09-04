using System.Text.Json;

namespace CodeJudge.Application.Common;

/// <summary>Shared serializer options for the structured <c>output</c> JSON stored on rubric results.</summary>
internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Output = new(JsonSerializerDefaults.Web);
}
