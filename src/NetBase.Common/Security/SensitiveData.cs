using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Unicode;

namespace NetBase.Common.Security;

/// <summary>
/// 敏感数据脱敏：对象序列化为 JSON 时将密码类字段替换为固定掩码。
/// 用于操作日志的参数记录，避免明文密码入库。
/// </summary>
public static class SensitiveData
{
    /// <summary>敏感字段词根：字段名包含任一词根即脱敏（覆盖 password/refreshToken/accessToken 等）</summary>
    private static readonly string[] SensitiveKeywords = ["password", "secret", "token", "credential"];

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) // 中文不转义（与 API 输出约定一致，日志可读）
    };

    private const string Mask = "***";

    /// <summary>序列化对象为 JSON，密码类字段脱敏，超长截断</summary>
    public static string Serialize(object? value, int maxLength = 2000)
    {
        if (value == null)
        {
            return string.Empty;
        }

        string json;
        try
        {
            var masked = MaskFields(JsonSerializer.SerializeToElement(value, Options));
            json = masked == null ? string.Empty : JsonSerializer.Serialize(masked, Options);
        }
        catch (JsonException)
        {
            json = value.ToString() ?? string.Empty;
        }

        return json.Length <= maxLength ? json : json[..maxLength] + "...(truncated)";
    }

    /// <summary>按词根判断字段是否敏感并返回掩码值（字段级审计用）</summary>
    public static object? MaskValue(string fieldName, object? value)
    {
        if (value == null) return null;
        return IsSensitive(fieldName) ? Mask : value;
    }

    private static bool IsSensitive(string fieldName)
    {
        foreach (var keyword in SensitiveKeywords)
        {
            if (fieldName.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>递归脱敏对象/数组中的敏感字段</summary>
    private static JsonElement? MaskFields(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                var dict = new Dictionary<string, JsonElement>();
                foreach (var property in element.EnumerateObject())
                {
                    if (IsSensitive(property.Name))
                    {
                        dict[property.Name] = JsonSerializer.SerializeToElement(Mask);
                    }
                    else
                    {
                        dict[property.Name] = MaskFields(property.Value) ?? property.Value;
                    }
                }
                return JsonSerializer.SerializeToElement(dict, Options);

            case JsonValueKind.Array:
                var list = new List<JsonElement?>();
                foreach (var item in element.EnumerateArray())
                {
                    list.Add(MaskFields(item));
                }
                return JsonSerializer.SerializeToElement(list);

            default:
                return element;
        }
    }
}
