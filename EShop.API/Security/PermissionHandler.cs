using EShop.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EShop.API.Security
{
    public class DynamicPermissionHandler : AuthorizationHandler<DynamicPermissionRequirement>
    {
        private readonly EShopDbContext _dbContext;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public DynamicPermissionHandler(EShopDbContext dbContext, IHttpContextAccessor httpContextAccessor)
        {
            _dbContext = dbContext;
            _httpContextAccessor = httpContextAccessor;
        }

        // 引入必要的命名空间

        protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, DynamicPermissionRequirement requirement)
        {
            if (context.User.Identity == null || !context.User.Identity.IsAuthenticated) return;

            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null) return;

            // 👇👇👇 核心超进化：不抓真实 URL，去抓底层路由引擎的“匹配模板”！
            var endpoint = httpContext.GetEndpoint() as RouteEndpoint;
            if (endpoint == null) return; // 如果不是一个有效的 API 端点，直接忽略

            // 拿到类似 "api/products/{id}" 的原始模板字符串
            var routeTemplate = endpoint.RoutePattern.RawText?.ToLower();

            // 补齐前面的斜杠，变成标准化格式 "/api/products/{id}"
            var normalizedTemplate = "/" + routeTemplate?.TrimStart('/');

            var httpMethod = httpContext.Request.Method.ToUpper();

            var userRoles = context.User.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();
            if (!userRoles.Any()) return;

            // 👇 极其精准的数据库比对：用标准化模板去查库！
            bool hasPermission = await _dbContext.RolePermissions
                .AnyAsync(p => userRoles.Contains(p.RoleName)
                            && p.Path.ToLower() == normalizedTemplate
                            && p.Method.ToUpper() == httpMethod);

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
        }
    }
}