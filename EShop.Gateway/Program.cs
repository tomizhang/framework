using Ocelot.DependencyInjection;
using Ocelot.Middleware;
using Ocelot.Provider.Consul;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 1. 加载 ocelot.json 配置文件
builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

// 定义当前服务的名字（在 Jaeger 界面里显示的分类名）
// 网关项目写 "EShop.Gateway"，API 项目写 "EShop.API"
var serviceName = builder.Environment.ApplicationName;

builder.Services.AddOpenTelemetry()
    .WithTracing(tracerProviderBuilder =>
    {
        tracerProviderBuilder
            .AddSource(serviceName)
            .ConfigureResource(resource => resource.AddService(serviceName))
            // 1. 自动记录所有进入该微服务的 HTTP 请求
            .AddAspNetCoreInstrumentation()
            // 2. 自动记录所有由该微服务发出的 HTTP 请求 (它会自动把 TraceId 塞进请求头传给下游！)
            .AddHttpClientInstrumentation()
            // 3. 把收集到的追踪数据，通过 OTLP gRPC 协议发送给本地的 Jaeger 侦探
            .AddOtlpExporter(opts =>
            {
                opts.Endpoint = new Uri("http://localhost:4317");
            });
    });

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