using System.Text.Json;
using NetBase.Service.Sys.Flow;
using Xunit;

namespace NetBase.Tests;

/// <summary>审批流条件求值器测试：操作符语义、类型宽容、变量缺失兜底</summary>
public class FlowConditionTests
{
    private static JsonElement Vars(string json) => JsonDocument.Parse(json).RootElement;

    private static FlowCondition C(string variable, string op, object? value)
    {
        return new FlowCondition
        {
            Variable = variable,
            Op = op,
            Value = value == null
                ? null
                : JsonSerializer.SerializeToElement(value, FlowGraph.JsonOpts)
        };
    }

    [Fact]
    public void NumericComparison_ShouldWork()
    {
        var vars = Vars("""{"amount": 8000}""");

        Assert.True(ConditionEvaluator.Evaluate([C("amount", "lt", 10000)], vars));
        Assert.True(ConditionEvaluator.Evaluate([C("amount", "lte", 8000)], vars));
        Assert.True(ConditionEvaluator.Evaluate([C("amount", "gte", 8000)], vars));
        Assert.False(ConditionEvaluator.Evaluate([C("amount", "gt", 10000)], vars));
        // 数值与字符串数值的宽容比较（设计器产出的值常是字符串）
        Assert.True(ConditionEvaluator.Evaluate([C("amount", "eq", "8000")], vars));
    }

    [Fact]
    public void StringComparison_ShouldWork()
    {
        var vars = Vars("""{"dept": "研发部", "type": "travel"}""");

        Assert.True(ConditionEvaluator.Evaluate([C("dept", "eq", "研发部")], vars));
        Assert.True(ConditionEvaluator.Evaluate([C("dept", "contains", "研发")], vars));
        Assert.True(ConditionEvaluator.Evaluate([C("type", "in", new[] { "travel", "meal" })], vars));
        Assert.False(ConditionEvaluator.Evaluate([C("dept", "ne", "研发部")], vars));
    }

    [Fact]
    public void AndGroup_ShouldRequireAll()
    {
        var vars = Vars("""{"amount": 5000, "type": "meal"}""");

        Assert.True(ConditionEvaluator.Evaluate(
            [C("amount", "lt", 10000), C("type", "eq", "meal")], vars));
        Assert.False(ConditionEvaluator.Evaluate(
            [C("amount", "lt", 10000), C("type", "eq", "travel")], vars));
    }

    [Fact]
    public void MissingVariable_ShouldNotMatch()
    {
        var vars = Vars("""{"amount": 5000}""");

        // 变量未提供 = 不命中（走默认分支），而非异常
        Assert.False(ConditionEvaluator.Evaluate([C("deptId", "eq", 1)], vars));
        Assert.False(ConditionEvaluator.Evaluate([C("deptId", "ne", 1)], vars));
    }

    [Fact]
    public void UnknownOperator_ShouldNotMatch()
    {
        var vars = Vars("""{"amount": 5000}""");

        Assert.False(ConditionEvaluator.Evaluate([C("amount", "regex", ".*")], vars));
    }

    [Fact]
    public void EmptyConditions_ShouldMatch()
    {
        var vars = Vars("{}");

        Assert.True(ConditionEvaluator.Evaluate([], vars));
    }
}
