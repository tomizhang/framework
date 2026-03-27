using System.Text;
using System.Text.Json;
using EShop.MessageCenter.Hubs;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EShop.MessageCenter.Services
{
    // 继承 BackgroundService，让它随着微服务启动而在后台极其安静地永远运行
    public class RabbitMqListenerService : BackgroundService
    {
        private readonly IHubContext<NotificationHub> _hubContext;
        private IConnection _connection;
        private IModel _channel;

        // 👇 极其霸气：把信号塔的控制台直接注入进来！
        public RabbitMqListenerService(IHubContext<NotificationHub> hubContext)
        {
            _hubContext = hubContext;
            InitRabbitMq();
        }

        private void InitRabbitMq()
        {
            // 连接到你的 RabbitMQ 服务器 (这里用默认本地配置)
            var factory = new ConnectionFactory { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // 声明一个极其专属的消息队列
            _channel.QueueDeclare(queue: "eshop.notifications", durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            stoppingToken.ThrowIfCancellationRequested();

            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += async (ch, ea) =>
            {
                // 1. 从 MQ 里抓出上游业务微服务扔过来的消息
                var content = Encoding.UTF8.GetString(ea.Body.ToArray());
                Console.WriteLine($"[情报中心] 收到上游 MQ 密电: {content}");

                // 2. 解析密电 (极其简单的 JSON 反序列化)
                var message = JsonSerializer.Deserialize<NotificationMessage>(content);

                if (message != null)
                {
                    // 👇 3. 终极绝杀：右手瞬间按下 SignalR 广播按钮！
                    // 注意：这里为了演示，我们发给所有连着的人。
                    // 真实场景你可以用 _hubContext.Clients.User(message.UserId) 进行精准打击！
                    await _hubContext.Clients.All.SendAsync(message.MessageType, message.Content);
                    Console.WriteLine($"[信号塔] 已通过 WebSocket 将指令 {message.MessageType} 轰炸至前端！");
                }

                // 4. 确认消息已处理
                _channel.BasicAck(ea.DeliveryTag, false);
            };

            // 开始极其耐心地监听
            _channel.BasicConsume("eshop.notifications", false, consumer);

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
            base.Dispose();
        }
    }

    // 极其轻量的消息载体类
    public class NotificationMessage
    {
        public string UserId { get; set; } // 目标用户
        public string MessageType { get; set; } // 消息类型，比如 "ForceLogout" 或 "OrderShipped"
        public string Content { get; set; } // 具体内容
    }
}