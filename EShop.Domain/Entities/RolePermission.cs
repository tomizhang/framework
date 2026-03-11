using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EShop.Domain.Entities
{
    public class RolePermission
    {
        public int Id { get; set; }

        /// <summary>
        /// 角色名称 (例如: "Admin", "User", "ProductManager")
        /// </summary>
        public string RoleName { get; set; } = string.Empty;

        /// <summary>
        /// 允许访问的 API 路径 (例如: "/api/products")
        /// </summary>
        public string Path { get; set; } = string.Empty;

        /// <summary>
        /// 允许的 HTTP 方法 (例如: "GET", "POST", "PUT", "DELETE", 甚至 "*" 代表全部)
        /// </summary>
        public string Method { get; set; } = string.Empty;
    }
}
