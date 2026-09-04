using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetBase.Common.Extensions;

/// <summary>通用扩展方法</summary>
public static class CommonExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public static bool IsNullOrEmpty([NotNullWhen(false)] this string? value) => string.IsNullOrEmpty(value);

    public static bool IsNotNullOrEmpty([NotNullWhen(true)] this string? value) => !string.IsNullOrEmpty(value);

    public static bool IsNullOrWhiteSpace([NotNullWhen(false)] this string? value) => string.IsNullOrWhiteSpace(value);

    public static bool IsNotNullOrWhiteSpace([NotNullWhen(true)] this string? value) => !string.IsNullOrWhiteSpace(value);

    /// <summary>序列化为 JSON 字符串</summary>
    public static string ToJson<T>(this T value) => JsonSerializer.Serialize(value, JsonOptions);

    /// <summary>从 JSON 字符串反序列化，失败返回默认值</summary>
    public static T? FromJson<T>(this string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return default;
        }
    }
}
