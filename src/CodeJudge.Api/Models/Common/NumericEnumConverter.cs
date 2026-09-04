using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeJudge.Api.Models.Common;

/// <summary>
/// Serialises an enum as its underlying integer value, overriding the globally-registered
/// string enum converter. Used for <see cref="Application.Common.CodeJudgeErrorCode"/> so the
/// wire carries the stable numeric code (e.g. <c>3001</c>), while status/language/rubric enums
/// stay camelCase strings.
/// </summary>
public sealed class NumericEnumConverter<T> : JsonConverter<T>
    where T : struct, Enum
{
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => (T)Enum.ToObject(typeof(T), reader.GetInt32());

    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        => writer.WriteNumberValue(Convert.ToInt32(value));
}
