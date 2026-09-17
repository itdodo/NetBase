using System.Text.Json;

namespace NetBase.Service.Sys.Flow;

/// <summary>
/// 条件分支求值器：纯函数解释执行（变量+操作符+值），不引入动态编译。
/// 数值比较统一转 double；未定义变量视为不命中（分组 AND，组间按优先级由引擎遍历）。
/// </summary>
public static class ConditionEvaluator
{
    /// <summary>判断一组条件（AND）是否全部命中</summary>
    public static bool Evaluate(IEnumerable<FlowCondition> conditions, JsonElement variables)
    {
        return conditions.All(c => EvaluateOne(c, variables));
    }

    private static bool EvaluateOne(FlowCondition condition, JsonElement variables)
    {
        if (variables.ValueKind != JsonValueKind.Object
            || !variables.TryGetProperty(condition.Variable, out var actual))
        {
            return false; // 变量未提供 = 不命中（宁走默认分支也不抛异常阻断流程）
        }

        return condition.Op.ToLowerInvariant() switch
        {
            "eq" => Compare(actual, condition.Value) == 0,
            "ne" => Compare(actual, condition.Value) != 0,
            "gt" => Compare(actual, condition.Value) > 0,
            "gte" => Compare(actual, condition.Value) >= 0,
            "lt" => Compare(actual, condition.Value) < 0,
            "lte" => Compare(actual, condition.Value) <= 0,
            "in" => In(actual, condition.Value),
            "contains" => Contains(actual, condition.Value),
            _ => false // 未知操作符不命中
        };
    }

    /// <summary>数值/字符串统一比较：数值转 double 比较；类型不一致回退字符串比较</summary>
    private static int Compare(JsonElement actual, JsonElement? expected)
    {
        if (expected == null)
        {
            return actual.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined ? 0 : 1;
        }

        if (IsNumber(actual) && IsNumber(expected.Value))
        {
            var l = ToDouble(actual);
            var r = ToDouble(expected.Value);
            return l.CompareTo(r);
        }

        return string.CompareOrdinal(ToString(actual), ToString(expected.Value));
    }

    private static bool In(JsonElement actual, JsonElement? expected)
    {
        if (expected == null || expected.Value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        return expected.Value.EnumerateArray().Any(item => Compare(actual, item) == 0);
    }

    private static bool Contains(JsonElement actual, JsonElement? expected)
    {
        if (expected == null)
        {
            return false;
        }

        if (actual.ValueKind == JsonValueKind.Array)
        {
            return actual.EnumerateArray().Any(item => Compare(item, expected) == 0);
        }

        return ToString(actual).Contains(ToString(expected.Value), StringComparison.Ordinal);
    }

    private static bool IsNumber(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number;

    private static double ToDouble(JsonElement e) =>
        e.ValueKind == JsonValueKind.Number ? e.GetDouble() : 0;

    /// <summary>任意标量转展示字符串（bool 转 true/false，数字去无意义尾零）</summary>
    public static string ToString(JsonElement e) => e.ValueKind switch
    {
        JsonValueKind.String => e.GetString() ?? string.Empty,
        JsonValueKind.True => "true",
        JsonValueKind.False => "false",
        JsonValueKind.Number => e.GetDouble().ToString("0.############"),
        JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
        _ => e.GetRawText()
    };
}
