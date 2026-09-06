using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetBase.Common.Json;

/// <summary>
/// 雪花ID序列化转换器：long 写为字符串（防 JS Number 精度丢失），
/// 读取时兼容字符串与数字两种形态。仅标注在 ID 语义的 long 属性上，
/// 避免全局字符串化把 code/total 等数值也变成字符串。
/// </summary>
public class LongToStringConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType == JsonTokenType.String
            ? long.Parse(reader.GetString()!)
            : reader.GetInt64();
    }

    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.ToString());
    }
}
