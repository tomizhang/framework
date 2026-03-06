using EShop.Identity;
using EShop.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions; // 引用

var builder = WebApplication.CreateBuilder(args);

// 1. 配置数据库 (使用内存数据库，为了快速演示)
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseInMemoryDatabase(nameof(ApplicationDbContext));

    // 注册 OpenIddict 的实体 (Client, Authorization, Token 等)
    options.UseOpenIddict();
});

// 2. 配置 ASP.NET Core Identity (用户管理)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

// 3. 配置 OpenIddict (核心配置)
builder.Services.AddOpenIddict()

    // 3.1 核心组件配置
    .AddCore(options =>
    {
        // 告诉它使用 EF Core 存数据
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })

    // 3.2 服务端配置 (Server)
    .AddServer(options =>
    {
        // 允许的端点 (OpenIddict 不会自动生成 Controller，但会处理这些路由的请求)
        // 启用需要的端点 (Endpoints)
        options.SetTokenEndpointUris("/connect/token")
               .SetAuthorizationEndpointUris("/connect/authorize");

        // 允许的授权模式
        options.AllowPasswordFlow();       // 允许用账号密码换 Token
        options.AllowClientCredentialsFlow(); // 允许机器直接换 Token
        options.AllowRefreshTokenFlow();   // 允许刷新 Token

        // 注册签名证书 (开发环境用临时的，生产环境要导入真实证书)
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // 接入 ASP.NET Core 的管道
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough(); // 关键：把请求放行给我们的 Controller 处理
    })

    // 3.3 验证配置 (Validation)
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// 添加控制器 (OpenIddict 需要我们写 Controller)
builder.Services.AddControllers();

// --- 自动初始化数据的后台任务 (下面会写) ---
builder.Services.AddHostedService<TestDataWorker>();

var app = builder.Build();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();