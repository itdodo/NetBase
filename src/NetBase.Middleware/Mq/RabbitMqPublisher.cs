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

/// <summary>RabbitMQ 发布实现（RabbitMQ.Client 7.x 异步 API，通道懒创建）</summary>
public class RabbitMqPublisher : IRabbitMqPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options;
    private readonly Lazy<Task<IChannel>> _channel;

    public RabbitMqPublisher(RabbitMqOptions options)
    {
        _options = options;
        _channel = new Lazy<Task<IChannel>>(CreateChannelAsync);
    }

    private async Task<IChannel> CreateChannelAsync()
    {
        var factory = new ConnectionFactory { Uri = new Uri(_options.ConnectionString) };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();
        await channel.ExchangeDeclareAsync(_options.DefaultExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: CancellationToken.None);
        return channel;
    }

    public async Task PublishAsync<TMessage>(string routingKey, TMessage message, string? exchange = null, CancellationToken cancellationToken = default)
    {
        var channel = await _channel.Value;
        var body = Encoding.UTF8.GetBytes(message.ToJson());
        var properties = new BasicProperties
        {
            ContentType = MediaTypeNames.Application.Json,
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = Guid.NewGuid().ToString("N"),
            Timestamp = new AmqpTimestamp(DateTimeOffset.Now.ToUnixTimeSeconds())
        };
        await channel.BasicPublishAsync(exchange ?? _options.DefaultExchange, routingKey, false, properties, body, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel.IsValueCreated)
        {
            var channel = await _channel.Value;
            await channel.DisposeAsync();
        }
    }
}
