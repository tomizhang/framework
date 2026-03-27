using EShop.MessageCenter.Hubs;
using EShop.MessageCenter.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. 注册 SignalR 服务
builder.Services.AddSignalR();

// 2. 极其核心：把我们的 MQ 监听器注册为后台长驻服务！
builder.Services.AddHostedService<RabbitMqListenerService>();

var app = builder.Build();

// 3. 暴露唯一的长连接路由端口
app.MapHub<NotificationHub>("/hubs/notification");

app.Run();