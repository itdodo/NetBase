using System.Collections.Concurrent;

namespace NetBase.Api.Hubs;

/// <summary>
/// 用户连接映射（单实例内存实现；多实例部署时切换 Redis + SignalR backplane）。
/// 支持同一用户多端登录（一个用户多个连接）。
/// </summary>
public interface IUserConnectionMapping
{
    void Add(long userId, string connectionId);

    void Remove(long userId, string connectionId);

    /// <summary>用户当前全部连接ID（不在线返回空）</summary>
    IReadOnlyCollection<string> GetConnections(long userId);

    bool IsOnline(long userId);

    /// <summary>当前在线用户ID集合</summary>
    IReadOnlyCollection<long> GetOnlineUserIds();
}

public class UserConnectionMapping : IUserConnectionMapping
{
    private readonly ConcurrentDictionary<long, ConcurrentDictionary<string, byte>> _map = new();

    public void Add(long userId, string connectionId)
    {
        var connections = _map.GetOrAdd(userId, _ => new ConcurrentDictionary<string, byte>());
        connections[connectionId] = 0;
    }

    public void Remove(long userId, string connectionId)
    {
        if (!_map.TryGetValue(userId, out var connections)) return;
        connections.TryRemove(connectionId, out _);
        if (connections.IsEmpty) _map.TryRemove(userId, out _);
    }

    public IReadOnlyCollection<string> GetConnections(long userId) =>
        _map.TryGetValue(userId, out var c) ? c.Keys.ToArray() : [];

    public bool IsOnline(long userId) => _map.ContainsKey(userId);

    public IReadOnlyCollection<long> GetOnlineUserIds() => _map.Keys.ToArray();
}
