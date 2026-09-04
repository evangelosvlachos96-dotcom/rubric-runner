using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeJudge.Api.Models.Common;

/// <summary>
/// Serializer options that mirror the MVC pipeline's configuration, for the few places that write
/// JSON directly (the exception middleware and the API-key challenge) rather than via a result.
/// </summary>
public static class ApiJson
{
    public static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
        return options;
    }
}
