using System.Text.Json;
using NetBase.Common.Security;

namespace NetBase.Common.Auditing;

/// <summary>字段级变更审计：实体更新前后属性 diff（敏感字段脱敏）</summary>
public static class AuditDiff
{
    /// <summary>不参与 diff 的属性（框架审计/并发字段）</summary>
    private static readonly HashSet<string> Ignored = new(StringComparer.OrdinalIgnoreCase)
    {
        "Version", "UpdateTime", "UpdateBy", "IsDeleted"
    };

    /// <summary>
    /// 对比更新前后实体公共属性，生成变更明细。
    /// 返回 JSON 字符串（{"字段":{"old":..,"new":..}}），无差异返回 null。
    /// </summary>
    public static string? Diff(object before, object after)
    {
        var beforeProps = before.GetType().GetProperties().ToDictionary(p => p.Name, p => p);
        var changes = new Dictionary<string, object>();

        foreach (var prop in after.GetType().GetProperties())
        {
            if (Ignored.Contains(prop.Name) || !beforeProps.TryGetValue(prop.Name, out var beforeProp)) continue;
            if (!prop.CanRead || !beforeProp.CanRead) continue;

            var oldVal = beforeProp.GetValue(before);
            var newVal = prop.GetValue(after);

            if (Equals(oldVal, newVal)) continue;

            changes[prop.Name] = new
            {
                old = SensitiveData.MaskValue(prop.Name, oldVal),
                @new = SensitiveData.MaskValue(prop.Name, newVal)
            };
        }

        return changes.Count == 0 ? null : SensitiveData.Serialize(changes, maxLength: 8000);
    }
}
