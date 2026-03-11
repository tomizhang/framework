using Microsoft.AspNetCore.Authorization;

namespace EShop.API.Security
{
    // 这是一张“万能门票”，不再硬编码任何权限字符串
    public class DynamicPermissionRequirement : IAuthorizationRequirement
    {
    }
}