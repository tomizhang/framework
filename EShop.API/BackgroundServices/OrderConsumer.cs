using EShop.Application.Orders.Dtos;
using EShop.Domain.Entities;
using EShop.Infrastructure.Data;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace EShop.API.BackgroundServices
{
    public class OrderConsumer : BackgroundService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        // 关键点：注入 Scope 工厂，而不是直接注入 DbContext
        private readonly IServiceScopeFactory _scopeFactory;

        public OrderConsumer(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;

            var factory = new ConnectionFactory { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.QueueDeclare(queue: "orders", durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumer = new EventingBasicConsumer(_channel);

            consumer.Received += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var message = Encoding.UTF8.GetString(body);
                var orderEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(message);

                Console.WriteLine($"[MQ收到] 用户{orderEvent.UserId} 购买商品 {orderEvent.ProductId}");

                // 🌟 核心：手动创建一个 Scope (作用域)
                // 就像在 Controller 里处理一次 HTTP 请求一样
                using (var scope = _scopeFactory.CreateScope())
                {
                    // 从这个 Scope 里拿 DbContext
                    var dbContext = scope.ServiceProvider.GetRequiredService<EShopDbContext>();

                    // 1. 开启事务 (可选，为了严谨)
                    using var transaction = await dbContext.Database.BeginTransactionAsync();
                    try
                    {
                        // 2. 写入数据库
                        var order = new Order(orderEvent.ProductId, orderEvent.Quantity, orderEvent.UserId);
                        dbContext.Orders.Add(order);

                        await dbContext.SaveChangesAsync(); // 保存
                        await transaction.CommitAsync();    // 提交事务

                        Console.WriteLine($"[MQ处理成功] 订单已落库，ID: {order.Id}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MQ处理失败] {ex.Message}");
                        // 真实场景这里需要做"死信队列"重试
                    }
                }
            };

            _channel.BasicConsume(queue: "orders", autoAck: true, consumer: consumer);
            return Task.CompletedTask;
        }

        // ... Dispose 代码保持不变 ...
    }
}