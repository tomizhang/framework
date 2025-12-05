using EShop.Application.Common.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace EShop.Infrastructure.Messaging
{
    public class RabbitMQProducer : IMessageProducer
    {
        public void SendMessage<T>(T message)
        {
            // 1. 创建连接工厂
            var factory = new ConnectionFactory { HostName = "localhost" }; // 如果在Docker里互联可能需要改Host

            // 2. 建立连接和通道
            using var connection = factory.CreateConnection();
            using var channel = connection.CreateModel();

            // 3. 声明队列 (Queue)
            // durable: false (重启后队列是否还在？为了演示先false)
            // exclusive: false (是否只有当前连接能用？)
            // autoDelete: false (没消费者是否自动删除？)
            channel.QueueDeclare(queue: "orders", durable: false, exclusive: false, autoDelete: false, arguments: null);

            // 4. 序列化消息
            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            // 5. 发送消息
            channel.BasicPublish(exchange: "", routingKey: "orders", basicProperties: null, body: body);
        }
    }
}