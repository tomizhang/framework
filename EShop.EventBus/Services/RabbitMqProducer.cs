using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace EShop.EventBus
{
    public interface IMessageProducer
    {
        Task SendMessageAsync<T>(T message, string queueName = "eshop.notifications");
    }

    public class RabbitMqProducer : IMessageProducer
    {
        private readonly string _hostName;

        public RabbitMqProducer(string hostName)
        {
            _hostName = hostName;
        }

        public async Task SendMessageAsync<T>(T message, string queueName = "eshop.notifications")
        {
            var factory = new ConnectionFactory { HostName = _hostName };

            // 👇 1. 极其硬核：最新版全部换成 CreateConnectionAsync！
            await using var connection = await factory.CreateConnectionAsync();
            // 👇 2. 最新版连 Model 这个词都弃用了，改叫 Channel！
            await using var channel = await connection.CreateChannelAsync();

            // 👇 3. 队列声明也是纯异步的
            await channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false, autoDelete: false, arguments: null);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // 👇 4. 极其无情地异步扣动扳机！
            await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, body: body);
        }
    }
}
}