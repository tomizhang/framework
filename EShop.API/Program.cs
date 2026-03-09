using AutoMapper;
using EShop.Application.Auth;
using EShop.Application.Common.Caching;
using EShop.Application.Common.Interfaces;
using EShop.Application.Products;
using EShop.Infrastructure.Caching;
using EShop.Infrastructure.Data;
using EShop.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EShop.Infrastructure.Messaging;
using EShop.API.BackgroundServices;
using EShop.PricingService.Protos;
using Serilog;
using EShop.API.Filters;
using EShop.Infrastructure.Services;
using System.Text.Json;
using EShop.API;
using OpenTelemetry.Trace;
using OpenTelemetry.Resources;
using Serilog.Events; // 引用


// 👇 1. 在程序刚跑起来的第一行，就初始化 Serilog 的先遣队
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();


try
{

    Log.Information("正在启动 EShop.API 微服务...");
    var builder = WebApplication.CreateBuilder(args);

    // 👇 2. 极其关键：用 Serilog 彻底替换掉系统默认的日志组件
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        // 这里的 outputTemplate 是魔法！我们把 OpenTelemetry 的 TraceId 也打印出来！
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [TraceId: {TraceId}] {Message:lj}{NewLine}{Exception}"));

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

    // Add services to the container.

    builder.Services.AddControllers(options =>
    {
        // 添加全局过滤器
        // 这样每个 Controller 的每个方法都会经过这个 Filter
        options.Filters.Add<LogTimeFilter>();
    });
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //// 2. 告诉 .NET Core 使用 Serilog 替换默认日志
    //builder.Host.UseSerilog((context, services, configuration) => configuration
    //    .ReadFrom.Configuration(context.Configuration) // 读取 appsettings.json
    //    .ReadFrom.Services(services)
    //    .Enrich.FromLogContext());


    builder.Services.AddAutoMapper(typeof(EShop.Application.EShopApplicationAutoMapperProfile));
    //jwt ,
    builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
    builder.Services.AddScoped<ITokenService, JwtTokenService>();
    //auth
    builder.Services.AddScoped<IAuthService, AuthService>();

    builder.Services.AddScoped<IProductAppService, ProductAppService>();
    // 只要有人要 ICacheService，就给他 RedisCacheService
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
    // 这是一个高性能的长连接对象，整个应用只需一个实例
    builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
    {
        var configuration = ConfigurationOptions.Parse(builder.Configuration.GetConnectionString("Redis"));
        return ConnectionMultiplexer.Connect(configuration);
    });
    // 注册 Redis 缓存
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = builder.Configuration.GetConnectionString("Redis");
        options.InstanceName = "EShop_"; // 缓存键的前缀，防止和其他系统冲突
    });
    // 注册分布式锁服务
    builder.Services.AddScoped<IRedisLockService, RedisLockService>();
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

    // 2. 注册 DbContext
    // 告诉系统：我要用 PostgreSQL，连接字符串是上面那个
    builder.Services.AddDbContext<EShopDbContext>(options =>
        options.UseNpgsql(connectionString));

    builder.Services.AddHttpClient<WeChatAuthService>();

    // 2. 添加认证服务

    builder.Services.AddAuthentication(options =>
    {
        // 👇 这两行非常关键！告诉系统默认都用 JWT
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        #region 统一认证中心
        options.Authority = "https://localhost:5001";
        options.RequireHttpsMetadata = false;
        // 👇👇👇 击杀隐藏 Boss：无视本地 HTTPS 证书安全警告，强制放行请求 👇👇👇
        //options.BackchannelHttpHandler = new HttpClientHandler
        //{
        //    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        //};
        // 👇👇👇 绝对核心：告诉 API 闭嘴，无视一切 HTTPS 证书报错，强行把公钥给我下载下来！ 👇👇👇
        //options.BackchannelHttpHandler = new HttpClientHandler
        //{
        //    ServerCertificateCustomValidationCallback = delegate { return true; }
        //};
        options.TokenValidationParameters = new TokenValidationParameters
        {
            //https://localhost:5001/.well-known/jwks
            ValidIssuer = "https://localhost:5001",
            ValidateIssuerSigningKey = false,
            ValidateAudience = false,
            ValidateIssuer = false,
            ClockSkew = new TimeSpan(5),
            ValidTypes = new[] { "at+jwt" },
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("MySuperSecretKey_MustBeLongerThan16Chars"))

        };
        #endregion
        #region jwt api单体认证
#if DEBUG && FALSE
        options.RequireHttpsMetadata = false; // 开发环境允许 http
        options.SaveToken = true;
        var key = Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!);
        options.RequireHttpsMetadata = false;
        options.SaveToken = true;
        //options.TokenValidationParameters = new TokenValidationParameters
        //{
        //    ValidateIssuerSigningKey = true,
        //    IssuerSigningKey = new SymmetricSecurityKey(key),
        //    ValidateIssuer = true,
        //    ValidIssuer = builder.Configuration["Jwt:Issuer"],
        //    ValidateAudience = true,
        //    ValidAudience = builder.Configuration["Jwt:Audience"],
        //    ValidateLifetime = true,
        //    ClockSkew = TimeSpan.Zero
        //};
     
#endif
        #endregion
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                // 这里会把具体的错误打印到控制台
                Console.WriteLine("---------------------------------------------");
                Console.WriteLine($"[JWT 验证失败] 原因: {context.Exception.Message}");
                Console.WriteLine("---------------------------------------------");
                return Task.CompletedTask;
            },
            OnTokenValidated = context =>
            {
                Console.WriteLine("---------------------------------------------");
                Console.WriteLine($"[JWT 验证成功] 用户: {context.Principal.Identity.Name}");
                Console.WriteLine("---------------------------------------------");
                return Task.CompletedTask;
            }
        };
    });

    builder.Services.AddSwaggerGen(c =>
    {
        // 定义安全定义 (告诉 Swagger 我们用的是 Bearer Token)
        c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Name = "Authorization",
            Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey,
            Scheme = "Bearer",
            BearerFormat = "JWT",
            In = Microsoft.OpenApi.Models.ParameterLocation.Header,
            Description = "在下方输入框输入: Bearer {你的Token}"
        });

        // 应用安全需求 (告诉 Swagger 所有接口都可以尝试带 Token)
        c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            new string[] {}
        }
    });
    });
    //消费队列
    builder.Services.AddScoped<IMessageProducer, RabbitMQProducer>();
    builder.Services.AddHostedService<OrderConsumer>();

    //微服务rpc
    builder.Services.AddGrpcClient<Pricing.PricingClient>(o =>
    {
        // ⚠️ 这里填 EShop.PricingService 启动的 HTTPS 地址
        o.Address = new Uri("https://localhost:7194");
    });

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseRouting();

    // ✅ 注册全局异常中间件
    app.UseMiddleware<EShop.API.Middlewares.GlobalExceptionMiddleware>();

    // 3. 添加请求日志中间件 (这个很强，会自动记录每个 HTTP 请求的耗时、状态码)
    app.UseSerilogRequestLogging();


    // 👇 必须在 UseRouting 之后，UseAuthorization 之前
    app.UseAuthentication(); // 先验票（你是谁？）
    app.UseAuthorization();  // 再安检（你能进吗？）

    app.MapControllers();

    // 👇 添加一个极简的健康检查端点，告诉中介 "我还活着"
    app.MapGet("/health", () => Results.Ok("I am alive!"));

    // 👇 激活 Consul 自动注册
    app.RegisterConsul(app.Configuration, app.Lifetime);

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "EShop API 启动因意外错误终止！");
}
finally
{
    Log.CloseAndFlush(); // 确保日志都写进硬盘再退出
}

async Task<JsonWebKeySet> RetraveSigningKes()
{
    //https://localhost:5001/.well-known/jwks
    var keys = await new HttpClient().GetStringAsync("https://localhost:5001/.well-known/jwks");
    var res = JsonSerializer.Deserialize<JsonWebKeySet>(keys);
    return res;
}
