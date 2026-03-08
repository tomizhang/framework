using EShop.Identity.Data;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace EShop.Identity
{
    public class TestDataWorker : IHostedService
    {
        private readonly IServiceProvider _serviceProvider;

        public TestDataWorker(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            using var scope = _serviceProvider.CreateScope();

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await context.Database.EnsureCreatedAsync();

            var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

            // 注册 SSO 专用客户端
            if (await manager.FindByClientIdAsync("eshop_web_spa") == null)
            {
                await manager.CreateAsync(new OpenIddictApplicationDescriptor
                {
                    ClientId = "eshop_web_spa",
                    // 对于纯前端 SPA (Vue/React)，甚至可以不需要 Secret，只需配置 RedirectUris
                    ClientSecret = "spa_secret",
                    DisplayName = "EShop 前端商城网站",

                    // 👇 极其关键：登录成功后，认证中心要把授权码发到哪个网址？
                    // 假设你的前端运行在 7002 端口
                    RedirectUris = { new Uri("http://localhost:7002/signin-oidc") },
                    PostLogoutRedirectUris = { new Uri("http://localhost:7002/signout-callback-oidc") },

                    Permissions =
            {
                // 端点权限
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                
                // 模式权限：允许使用授权码模式 (SSO 的灵魂)
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                
                // 允许申请的 Scope
                OpenIddictConstants.Permissions.Prefixes.Scope + "eshop.api",
                OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                OpenIddictConstants.Permissions.Prefixes.Scope + "profile"
            }
                });
            }

            // 👇👇👇 【核心修改】推土机模式：先查找，如果有就删掉 👇👇👇
            var client = await manager.FindByClientIdAsync("postman_client");
            if (client != null)
            {
                await manager.DeleteAsync(client); // 删掉旧的
            }

            // 👇👇👇 重新创建 (名字还叫 postman_client，不用改 v2 了) 👇👇👇
            await manager.CreateAsync(new OpenIddictApplicationDescriptor
            {
                ClientId = "postman_client", // 这里的名字必须和 Postman 里填的一样
                ClientSecret = "secret",
                DisplayName = "Postman Client",
                Permissions ={
                        // 1. 允许访问端点
                        OpenIddictConstants.Permissions.Endpoints.Token,

                        // 2. 允许的授权模式
                        OpenIddictConstants.Permissions.GrantTypes.Password,
                        OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                        OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                        // 3. 允许的 Scope (权限范围) 👇 把这些都加上
                        OpenIddictConstants.Permissions.Prefixes.Scope + "client_app", // 修复了拼写
                        OpenIddictConstants.Permissions.Prefixes.Scope + "eshop.api",  // 👈 允许申请 eshop.api
                        OpenIddictConstants.Permissions.Prefixes.Scope + "openid",     // 👈 允许申请 openid (标准)
                        OpenIddictConstants.Permissions.Prefixes.Scope + "profile"     // 👈 允许申请 profile (标准)
                    }

            });

            // 创建测试用户 (保持不变)
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
            if (await userManager.FindByNameAsync("admin") == null)
            {
                var user = new IdentityUser { UserName = "admin", Email = "admin@eshop.com" };
                await userManager.CreateAsync(user, "Password123!");
            }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}