using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EShop.Identity.Data
{
    // 这里我们同时继承 IdentityDbContext (管理用户) 
    // OpenIddict 会自动在这里面挂载它需要的表
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options) { }
    }
}