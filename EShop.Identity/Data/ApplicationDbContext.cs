using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EShop.Identity.Data
{
    // 极其标准的继承：让它天生自带 AspNetUsers 等用户表
    public class ApplicationDbContext : IdentityDbContext<IdentityUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // 极其致命的细节：必须先调用 base，否则 Identity 的主键映射会当场炸毁！
            base.OnModelCreating(builder);

            // 👇 极其霸气的魔法：一句话把 OpenIddict 需要的所有 Token 表、应用表全盘注入进你的上下文！
            builder.UseOpenIddict();
        }
    }
}