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
using Serilog.Events;
using EShop.API.Security;
using Microsoft.AspNetCore.Authorization;
using OpenTelemetry.Metrics; // 引用
using Winton.Extensions.Configuration.Consul;
using System.Security.Claims;


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
        .Enrich.WithMachineName() // 👈 让每一条日志都自动带上 MachineName 属性！
                                  // 这里的 outputTemplate 是魔法！我们把 OpenTelemetry 的 TraceId 也打印出来！
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [TraceId: {TraceId}] {Message:lj}{NewLine}{Exception}")
    // 👇👇👇 新增这行极其关键的代码：把日志同时发送给 5341 端口的 Seq 👇👇👇
        .WriteTo.Seq("http://localhost:5341"));
    // 定义当前服务的名字（在 Jaeger 界面里显示的分类名）
    // 网关项目写 "EShop.Gateway"，API 项目写 "EShop.API"
    var serviceName = builder.Environment.ApplicationName;

    // 👇 极其关键：系统默认是不提供 HttpContext 的，必须手动把这个“雷达”注册进 DI 容器里！
    builder.Services.AddHttpContextAccessor();

    // 👇 极其霸气的大厂标准：给所有调用下游的 HTTP 客户端套上“防弹衣”！
    builder.Services.AddHttpClient("DownstreamServiceClient", client =>
    {
        // 假设这是你要调用的下游服务地址
        client.BaseAddress = new Uri("http://localhost:5001");
    })
    // 极其核心的魔法：一键注入大厂标准弹性策略（包含了超时、重试、熔断三板斧！）
    .AddStandardResilienceHandler(options =>
    {
        // 👇 1. 【新增核心配置】单次请求的超时时间（假设设为 3 秒）
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(3);

        // 👇 2. 【修复报错的核心】熔断器的统计窗口必须 >= 3秒的2倍（也就是至少 6 秒，我们设为 10 秒，极其安全）
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(10);

        // 失败率超过 10% 就会触发熔断拉闸
        options.CircuitBreaker.FailureRatio = 0.1;
        options.CircuitBreaker.BreakDuration = TimeSpan.FromSeconds(30);

        // 全局总超时（包含所有重试加起来的时间，通常要比 AttemptTimeout 大得多）
        options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(15);
    });

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
        })
        .WithMetrics(metrics =>
        {
            metrics
                        // 1. 【修复 CS1929】直接监听 .NET 8 内置的 API 和 Web 服务器原声指标！
                        .AddMeter("Microsoft.AspNetCore.Hosting")
                        .AddMeter("Microsoft.AspNetCore.Server.Kestrel")

                        // 2. 运行时底层监控（CPU、内存等）保持不变
                        .AddRuntimeInstrumentation()

                        // 3. 导出给 Prometheus
                        .AddPrometheusExporter();
        }); ;

    // Add services to the container.

    builder.Services.AddControllers(options =>
    {
        // 添加全局过滤器
        // 这样每个 Controller 的每个方法都会经过这个 Filter
        options.Filters.Add<LogTimeFilter>();
        options.Filters.Add<GlobalResponseFilter>();
    });
    // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen();

    //// 2. 告诉 .NET Core 使用 Serilog 替换默认日志
    //builder.Host.UseSerilog((context, services, configuration) => configuration
    //    .ReadFrom.Configuration(context.Configuration) // 读取 appsettings.json
    //    .ReadFrom.Services(services)
    //    .Enrich.FromLogContext());


    // 👇 极其霸气的分布式配置中心接入！
    var env = builder.Environment.EnvironmentName; // 获取当前环境 (比如 Development)
    builder.Configuration.AddConsul(
        $"EShop.API/appsettings.{env}.json", // 去 Consul 里找这个名字的配置 (一会我们要去建)
        options =>
        {
            // 1. 告诉它你的 Consul 指挥中心在哪里 (假设在本地 8500)
            options.ConsulConfigurationOptions = cco => { cco.Address = new Uri("http://localhost:8500"); };

            // 2. 极其核心：开启热更新！Consul 里一改，程序里瞬间生效，不用重启！
            options.ReloadOnChange = true;

            // 3. 如果 Consul 挂了，系统先别死，继续用本地的 appsettings.json 兜底
            options.Optional = true;

            // 4. 忽略初次连接失败时的异常
            options.OnLoadException = exceptionContext => { exceptionContext.Ignore = true; };
        }
    );

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
        options.UseNpgsql(connectionString,
        // 记得指路，告诉它迁移大本营在基础设施层
        b => b.MigrationsAssembly("EShop.Infrastructure")));

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
            RoleClaimType = "role", // 极其关键：告诉 API 哪个字段是角色
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
            OnTokenValidated =async context =>
            {
                Console.WriteLine("---------------------------------------------");
                Console.WriteLine($"[JWT 验证成功] 用户: {context.Principal.Identity.Name}");
                Console.WriteLine("---------------------------------------------");

                // 👇 极其核心的魔法：在请求上下文中，精准抓取你的 RedisCacheService！
                var redisService = context.HttpContext.RequestServices.GetRequiredService<ICacheService>();

                var userId = context.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    // 👇 去黑名单里查水表！(替换成你实际读取字符串的方法)
                    var isBlacklisted = await redisService.GetAsync<string>($"blacklist:user:{userId}");

                    if (!string.IsNullOrEmpty(isBlacklisted))
                    {
                        // 极其冷酷地拒绝他！
                        context.Fail("此账号已被管理员强制下线，请重新登录。");
                    }
                }

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

    // 👇 1. 注册我们的动态安检策略
    builder.Services.AddAuthorization(options =>
    {
        // 👇 2. 注册唯一的全局动态策略
        options.AddPolicy("DynamicRBAC", policy =>
        {
            policy.Requirements.Add(new DynamicPermissionRequirement());
        });
    });

    // 👇 2. 极其关键：把大队长注册到依赖注入容器里，这样他才能用到 EF Core 去查库！
    builder.Services.AddScoped<IAuthorizationHandler, DynamicPermissionHandler>();

    var app = builder.Build();

    // Configure the HTTP request pipeline.
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseRouting();

    // ✅ 注册全局异常中间件
    app.UseMiddleware<EShop.API.Middlewares.CommonMiddleware>();
    app.UseMiddleware<EShop.API.Middlewares.GlobalExceptionMiddleware>();

    // 3. 添加请求日志中间件 (这个很强，会自动记录每个 HTTP 请求的耗时、状态码)
    app.UseSerilogRequestLogging(options =>
    {
        // 每次 HTTP 请求结束，准备打印那条漂亮的 summary 日志时，都会触发这个钩子
        options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
        {
            // 1. 抓取请求方的真实 IP
            var ip = httpContext.Connection.RemoteIpAddress?.ToString();
            diagnosticContext.Set("ClientIP", string.IsNullOrEmpty(ip) ? "Unknown" : ip);

            // 2. 抓取客户端的设备信息 (比如是用 Chrome 还是 Postman 访问的)
            var userAgent = httpContext.Request.Headers["User-Agent"].FirstOrDefault();
            diagnosticContext.Set("UserAgent", userAgent);

            // 3. 抓取当前登录的用户名 (极度关键的业务信息！)
            // 只要你的 API 配置了 JWT 验证，并且前端带了 Token 过来，这里就能拿到！
            var userName = httpContext.User?.Identity?.IsAuthenticated == true
                ? httpContext.User.Identity.Name
                : "Guest"; // 没登录的统一叫游客

            diagnosticContext.Set("UserName", userName);

            // 4. 你还可以加更多：比如 CorrelationId、订单号、甚至请求的域名
            // diagnosticContext.Set("Host", httpContext.Request.Host.Value);
        };
    });


    // 👇 必须在 UseRouting 之后，UseAuthorization 之前
    app.UseAuthentication(); // 先验票（你是谁？）
    app.UseAuthorization();  // 再安检（你能进吗？）

    //app.UseOpenTelemetryPrometheusScrapingEndpoint();
    app.MapPrometheusScrapingEndpoint();
    app.MapControllers();

    // 👇 添加一个极简的健康检查端点，告诉中介 "我还活着"
    app.MapGet("/health", () => Results.Ok("I am alive!"));

    // 👇 激活 Consul 自动注册
    app.RegisterConsul(app.Configuration, app.Lifetime);

    app.Run();
}// 👇 加上这个极其关键的专属捕获块：过滤掉 EF Core 工具的正常中止动作！
catch (HostAbortedException)
{
    // EF Core 迁移工具在提取完元数据后会抛出此异常，属于正常现象，静默忽略即可。
    Log.Information("EF Core 工具提取配置完毕，正常中止 Host。");
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
