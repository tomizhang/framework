using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// 1. 加载 ocelot.json 配置文件
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// 2. 注册 Ocelot 服务
builder.Services.AddOcelot(builder.Configuration);

var app = builder.Build();

// 3. 启用 Ocelot 中间件
// await app.UseOcelot(); // 旧版写法
app.UseOcelot().Wait();   // 标准写法

app.Run();