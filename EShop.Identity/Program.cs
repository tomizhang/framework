using EShop.Identity;
using EShop.Identity.Data;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;
using System.Text; // 引用
using EShop.Identity.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    // 告诉 .NET：虽然我是 HTTP 接客，但外面的云端大佬已经做过 HTTPS 校验了！
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// 👇 1. 添加 CORS 策略：允许 7002 访问网关
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowSPA", policy =>
    {
        policy.WithOrigins("http://localhost:7002") // 允许前端地址
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// 1. 配置数据库 (使用内存数据库，为了快速演示)
//builder.Services.AddDbContext<ApplicationDbContext>(options =>
//{
//    options.UseInMemoryDatabase(nameof(ApplicationDbContext));

//    // 注册 OpenIddict 的实体 (Client, Authorization, Token 等)
//    options.UseOpenIddict();
//});

builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    // 读取连接字符串，连接咱们 docker-compose 里的 pg-database
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"));

    // 👇 极其关键：告诉 EF Core，你肚子里装了 OpenIddict 的实体！
    options.UseOpenIddict();
});

// 2. 配置 ASP.NET Core Identity (用户管理)
builder.Services.AddIdentity<IdentityUser, IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddAuthentication()
    .AddGitHub(options =>
    {
        // 填入你刚才在 GitHub 申请到的 ID 和 Secret
        //options.ClientId = "Ov23liTy27M0tSnrgCJH";
        options.ClientId = "Ov23liTy27M0tSnrgCJH";
        options.ClientSecret = "bf2d40a93ec5c77b4e7b2f9134ecb179bfa933c1";

        // 我们想获取用户的邮箱，所以加上这个 Scope
        options.Scope.Add("user:email");

        // 👇👇👇 补上这行绝对核心的代码：告诉 GitHub 使用 Identity 的外部登录机制接管 👇👇👇
        options.SignInScheme = IdentityConstants.ExternalScheme;
    });

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
        // 👇👇👇 必须显式声明这三个核心端点的门牌号 👇👇👇
        options.SetTokenEndpointUris("/connect/token")
               .SetAuthorizationEndpointUris("/connect/authorize")
               .SetEndSessionEndpointUris("/connect/logout");// 👇 极其关键：开启 UserInfo 档案查询端点

        options.SetUserInfoEndpointUris("/connect/userinfo");

        // 允许的授权模式
        options.AllowPasswordFlow();
        options.AllowClientCredentialsFlow();
        options.AllowRefreshTokenFlow();

        // SSO 核心模式
        options.AllowAuthorizationCodeFlow();
        options.RequireProofKeyForCodeExchange(); // 强制开启 PKCE (极致安全的行业标准)

        // 👇👇👇 【核心修复在这里】明确告诉服务器，这些 Scope 是合法的 👇👇👇
        options.RegisterScopes("client_app", "eshop.api", "openid", "profile");

        // 👇👇👇 新增这一行：关闭强加密，强制输出标准 JWT 👇👇👇
        options.DisableAccessTokenEncryption();
        var secretKey = "MySuperSecretKey_MustBeLongerThan16Chars";
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        options.AddSigningKey(securityKey);
        // 注册签名证书
        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        // 接入 ASP.NET Core 的管道
        options.UseAspNetCore()
               .EnableTokenEndpointPassthrough()
               // 👇👇👇 补上下面这行！告诉 OpenIddict 把授权请求放行给我们的 Controller 👇👇👇
               .EnableAuthorizationEndpointPassthrough()
               // 👇👇👇 既然在做 SSO，顺便把登出请求也放行了（如果用的是 OpenIddict 6.x，可能叫 EnableEndSessionEndpointPassthrough）👇👇👇
               .EnableEndSessionEndpointPassthrough()
               .EnableUserInfoEndpointPassthrough();
    })

    // 3.3 验证配置 (Validation)
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// 添加控制器 (OpenIddict 需要我们写 Controller)
//让项目支持 Controller 和 HTML 视图
builder.Services.AddControllersWithViews();

// --- 自动初始化数据的后台任务 (下面会写) ---
builder.Services.AddHostedService<TestDataWorker>();

var app = builder.Build();

// 👇 极其规范的作用域管理：用完即毁，绝不占用内存
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // 1. 获取你的数据库上下文 (如果你的名字叫 ApplicationDbContext 就换一下)
        var dbContext = services.GetRequiredService<ApplicationDbContext>();

        // 2. 极其暴力的绝杀：如果数据库没建就建，有没跑的 Migration 就全部跑完！
        // ⚠️ 极其关键：绝对不要用 EnsureCreated()！必须用 Migrate()，否则后续无法做迁移更新！
        dbContext.Database.Migrate();

        // 3. 极其丝滑的种子数据播种（比如初始化你的 eshop_web_spa 客户端）
        // var openIddictManager = services.GetRequiredService<IOpenIddictApplicationManager>();
        // await SeedDataAsync(openIddictManager); 

        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("极其完美：数据库初始化与迁移彻底完成！");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogCritical(ex, "极其惨烈：数据库自动迁移在启动时翻车了！");
        // 可以在这里 throw，让容器挂掉重启重试
        throw;
    }
}

app.UseForwardedHeaders();

app.UseCors("AllowSPA");

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();

app.Run();

