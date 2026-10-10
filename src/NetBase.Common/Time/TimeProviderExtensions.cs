namespace NetBase.Common.Time;

/// <summary>TimeProvider 取值扩展（收敛常用形态，调用点无需 new DateTimeOffset）</summary>
public static class TimeProviderGetExtensions
{
    /// <summary>当前本地时间（与 DateTime.Now 语义一致；不遮蔽 TimeProvider 原生 GetLocalNow）</summary>
    public static DateTime LocalNow(this TimeProvider tp) => tp.GetLocalNow().LocalDateTime;
}
