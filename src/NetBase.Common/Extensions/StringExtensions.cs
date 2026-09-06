namespace NetBase.Common.Extensions;

/// <summary>字符串通用扩展</summary>
public static class StringExtensions
{
    /// <summary>超长截断并追加省略标记（日志/落库字段长度保护用）</summary>
    public static string? TruncateTo(this string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }
        return value[..maxLength] + "...(truncated)";
    }
}
