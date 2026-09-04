namespace NetBase.Middleware.Mq;

/// <summary>RabbitMQ 配置（appsettings.json 的 RabbitMQ 节点）</summary>
public class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    /// <summary>是否启用，默认关闭（备用中间件）</summary>
    public bool Enabled { get; set; }

    /// <summary>连接串，如 amqp://guest:guest@localhost:5672</summary>
    public string ConnectionString { get; set; } = "amqp://guest:guest@localhost:5672";

    /// <summary>默认交换机</summary>
    public string DefaultExchange { get; set; } = "netbase.exchange";
}
