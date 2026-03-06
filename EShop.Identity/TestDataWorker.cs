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
                Permissions =
                {
                    // 1. 允许访问端点
                    OpenIddictConstants.Permissions.Endpoints.Token,
            
                    // 2. 允许的授权模式
                    OpenIddictConstants.Permissions.GrantTypes.Password,
                    OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                    OpenIddictConstants.Permissions.GrantTypes.RefreshToken,

                    // 3. 允许的 Scope (权限范围)
                    // ⚠️ 注意：这里必须和 Postman 里填的 scope 一一对应
                    OpenIddictConstants.Permissions.Prefixes.Scope + "clinet_app",
                    //OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                    //OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                    //OpenIddictConstants.Permissions.Prefixes.Scope + "offline_access"
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