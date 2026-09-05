using System.Net.Mime;
using System.Text;
using NetBase.Common.Extensions;
using RabbitMQ.Client;

namespace NetBase.Middleware.Mq;

/// <summary>
/// RabbitMQ 消息发布接口（备用中间件，配置默认关闭）。
/// </summary>
public interface IRabbitMqPublisher
{
    /// <summary>发布消息（JSON 序列化，持久化投递）</summary>
    Task PublishAsync<TMessage>(string routingKey, TMessage message, string? exchange = null, CancellationToken cancellationToken = default);
}

/// <summary>
/// RabbitMQ 发布实现（RabbitMQ.Client 7.x 异步 API）。
/// 通道懒创建；连接失败后自动重建，不会永久缓存失败状态。
/// </summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile Task<IChannel>? _channelTask;

    public RabbitMqPublisher(RabbitMqOptions options)
    {
        _options = options;
    }

    private async Task<IChannel> CreateChannelAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(_options.DefaultExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: CancellationToken.None);
        return channel;
    }

    private async Task<IChannel> GetChannelAsync()
    {
        var current = _channelTask;
        if (current is { IsCompletedSuccessfully: true })
        {
            return current.Result;
        }

        await _gate.WaitAsync();
        try
        {
            current = _channelTask;
            if (current is { IsCompletedSuccessfully: true })
            {
                return current.Result;
            }

            // 未创建或已失败：重建通道（失败任务不缓存，避免首次连接失败后永久不可用）
            var created = CreateChannelAsync();
            _channelTask = created;
            return await created.ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task PublishAsync<TMessage>(string routingKey, TMessage message, string? exchange = null, CancellationToken cancellationToken = default)
    {
        var channel = await GetChannelAsync().ConfigureAwait(false);
        var body = Encoding.UTF8.GetBytes(message.ToJson());
        var properties = new BasicProperties
        {
            ContentType = MediaTypeNames.Application.Json,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.Now.ToUnixTimeSeconds())
        };
        await channel.BasicPublishAsync(exchange ?? _options.DefaultExchange, routingKey, false, properties, body, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        var current = _channelTask;
        if (current is { IsCompletedSuccessfully: true })
        {
            await current.Result.DisposeAsync().ConfigureAwait(false);
        }
        _gate.Dispose();
    }
}
