using EShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EShop.Infrastructure.Data
{
    // 继承自 DbContext，这是 EF Core 的核心基类
    public class EShopDbContext : DbContext
    {
        // 1. 构造函数
        // options 参数包含了"连接到哪个数据库"、"用什么账号密码"等配置
        // 这里的 : base(options) 是把参数传给父类 DbContext
        public EShopDbContext(DbContextOptions<EShopDbContext> options) : base(options)
        {
        }

        // 2. 定义数据表
        // 这一行代码意味着数据库里将会有一张叫 "Products" 的表
        public DbSet<Product> Products { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Order> Orders { get; set; }
        // 3. 实体配置 (Fluent API)
        // 推荐在这里配置数据库规则，保持实体类纯净
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 配置 Product 实体
            modelBuilder.Entity<Product>(entity =>
            {
                // 设置表名 (如果不设置，默认就是 DbSet 的属性名 Products)
                entity.ToTable("Products");

                // 设置主键
                entity.HasKey(e => e.Id);

                // 设置属性限制
                entity.Property(e => e.Name)
                    .IsRequired()       // 必填 (Not Null)
                    .HasMaxLength(100); // 最大长度 100


                // 在配置 Product 的代码块里添加：
                entity.Property(e => e.ImageUrl)
                      .IsRequired(false); // ✅ 显式设置为：不需要必填

                entity.Property(e => e.Price)
                    .HasColumnType("decimal(18,2)"); // 设置精度，非常重要！防止钱变成奇怪的小数

                // 设置软删除过滤器 (高级特性！)
                // 这样当你查询 products 时，EF Core 自动帮你加上 "WHERE IsDeleted = false"
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            modelBuilder.Entity<User>(entity => {
                entity.HasIndex(u => u.Username).IsUnique(); // 用户名唯一索引
                entity.Property(u => u.Username).HasMaxLength(50).IsRequired();
                entity.Property(u => u.Email).HasMaxLength(100);
            });
        }
    }
}