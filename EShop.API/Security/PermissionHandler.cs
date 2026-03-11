using EShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EShop.API.Security
{
    public class DynamicPermissionHandler : AuthorizationHandler<DynamicPermissionRequirement>
    {
        private readonly EShopDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor; // 👈 召唤雷达

        public DynamicPermissionHandler(EShopDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DynamicPermissionRequirement requirement)
        {
            if (context.User.Identity == null || !context.User.Identity.IsAuthenticated) return;

            // 1. 抓取当前请求的 URL 和 Method！
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            var requestPath = httpContext.Request.Path.Value?.ToLower(); // 比如："/api/products"
            var httpMethod = httpContext.Request.Method.ToUpper();       // 比如："POST"

            // 2. 从 Token 中掏出该用户所有的角色 (我们在 Identity 里塞进去的 role)
            var userRoles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (!userRoles.Any()) return;

            // 3. 终极查库：这个用户的任意一个角色，是否被允许用这个 Method 访问这个 Path？
            /* // 真实业务里的 EF Core 查库代码大概长这样：
            bool hasPermission = await _dbContext.RoleApiPermissions
                .AnyAsync(p => userRoles.Contains(p.RoleName) 
                            && p.ApiPath == requestPath 
                            && p.HttpMethod == httpMethod);
            */

            // 💡 模拟查库
            bool hasPermission = SimulateCheckDatabase(userRoles, requestPath, httpMethod);

            // 4. 宣判
            if (hasPermission)
            {
                context.Succeed(requirement); // 放行！
            }
        }

        private bool SimulateCheckDatabase(List<string> roles, string path, string method)
        {
            // 假设数据库里配置了：Admin 角色可以 POST /api/products
            if (roles.Contains("Admin") && path.StartsWith( "/api/products" )&& method == "GET")
            {
                return true;
            }
            return false;
        }
    }
}