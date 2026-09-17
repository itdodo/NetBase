using NetBase.Common.Realtime;
using NetBase.Model.Enums;
using NetBase.Service.Sys.Flow;

namespace NetBase.IntegrationTests;

/// <summary>通知测试替身：收集推送，验证审批通知链路</summary>
public sealed class FlowTestNotifications : INotifyService
{
    private readonly List<(IReadOnlyCollection<long> Users, NoticePayload Notice)> _pushes = [];

    public IReadOnlyList<(IReadOnlyCollection<long> Users, NoticePayload Notice)> Pushes => _pushes;

    public Task PushToUsersAsync(IReadOnlyCollection<long> userIds, NoticePayload notice, CancellationToken ct = default)
    {
        _pushes.Add((userIds, notice));
        return Task.CompletedTask;
    }

    public Task PushToOnlineAsync(NoticePayload notice, CancellationToken ct = default) => Task.CompletedTask;

    public Task PushForceLogoutAsync(long userId, string reason, CancellationToken ct = default) => Task.CompletedTask;

    public void Clear() => _pushes.Clear();
}

/// <summary>业务回调测试替身：记录终态回调（FlowCode=test_flow）</summary>
public sealed class FlowTestHandler : IFlowBusinessHandler
{
    public static readonly List<(long BusinessId, FlowInstanceStatus Status)> Finished = [];

    public string FlowCode => "*"; // 通配：任意测试流程均回调

    public Task<string> GetSummaryAsync(long businessId) => Task.FromResult($"测试单据#{businessId}");

    public Task OnFinishedAsync(long businessId, FlowInstanceStatus finalStatus)
    {
        Finished.Add((businessId, finalStatus));
        return Task.CompletedTask;
    }

    public static void Clear() => Finished.Clear();
}
