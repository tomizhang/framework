using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;

var builder = WebApplication.CreateBuilder(args);

// 1. 加载 ocelot.json 配置文件
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);
// ?? 1. 添加 CORS 策略：允许 7002 访问网关
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSPA", policy =>
    {
        policy.WithOrigins("http://localhost:7002") // 允许前端地址
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});
// 2. 注册 Ocelot 服务
// 把原来的 builder.Services.AddOcelot(); 改成：
builder.Services.AddOcelot().AddConsul(); // ?? 告诉 Ocelot 引入 Consul 寻址能力

var app = builder.Build();

// ?? 2. 启用 CORS (必须写在 UseOcelot 之前！)
app.UseCors("AllowSPA");
// 3. 启用 Ocelot 中间件
// await app.UseOcelot(); // 旧版写法
app.UseOcelot().Wait();   // 标准写法

app.Run();