using System;
using System.Diagnostics.CodeAnalysis;
using EShop.Domain.Common; // 引用刚才定义的基类

namespace EShop.Domain.Entities
{
    // 继承之前的 FullAuditedEntity
    public class Product : FullAuditedEntity<long>
    {
        // 1. 属性私有化 (Private Setters)
        // 外面只能看(get)，不能改(set)，想改必须走方法
        public string Name { get; private set; }
        public decimal Price { get; private set; }
        public int Stock { get; private set; } // 推荐用 int，方便做数学运算，负数通过逻辑控制
        public string Description { get; private set; }

        // 图片地址，可能为空
        public string? ImageUrl { get; private set; }

        // 2. 构造函数 (Constructor)
        // 强制要求：创建一个商品，必须有名字和价格。不允许创建一个"空"商品。
        private Product() { } // EF Core 需要一个无参构造函数，设为 private 即可

        public Product(string name, decimal price, int stock, string description)
        {
            // 守卫代码 (Guard Clauses)
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("商品名称不能为空");

            if (price < 0)
                throw new ArgumentException("价格不能为负数");

            if (stock < 0)
                throw new ArgumentException("库存不能为负数");

            Name = name;
            Price = price;
            Stock = stock;
            Description = description;
            ImageUrl = string.Empty;
        }

        // 3. 领域行为 (Domain Behaviors) - 也就是"方法"

        /// <summary>
        /// 修改价格
        /// </summary>
        public void ChangePrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException("价格不能为负数");

            // 这里还可以加入更复杂的逻辑，比如：记录价格变动历史
            Price = newPrice;
        }

        /// <summary>
        /// 扣减库存 (这是电商最核心的方法！)
        /// </summary>
        public void ReduceStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("扣减数量必须大于0");

            if (Stock < quantity)
                throw new InvalidOperationException("库存不足"); // 抛出业务异常

            Stock -= quantity;
        }

        /// <summary>
        /// 增加库存
        /// </summary>
        public void AddStock(int quantity)
        {
            if (quantity <= 0)
                throw new ArgumentException("增加数量必须大于0");

            Stock += quantity;
        }
    }
}