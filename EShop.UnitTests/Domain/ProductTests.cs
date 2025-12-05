using EShop.Domain.Entities;
using Xunit; // 引用 xUnit

namespace EShop.UnitTests.Domain
{
    public class ProductTests
    {
        [Fact] // 👈 告诉 xUnit 这是一个测试方法
        public void ReduceStock_Should_Decrease_Quantity_When_Stock_Is_Sufficient()
        {
            // 1. Arrange (准备数据)
            // 创建一个库存为 100 的商品
            var product = new Product("测试商品", 100m, 100, "描述");
            int reduceQuantity = 10;

            // 2. Act (执行动作)
            product.ReduceStock(reduceQuantity);

            // 3. Assert (断言结果)
            // 我们期望库存剩下 90
            Assert.Equal(90, product.Stock);
        }

        [Fact]
        public void ReduceStock_Should_Throw_Exception_When_Stock_Is_Insufficient()
        {
            // 1. Arrange
            var product = new Product("测试商品", 100m, 10, "描述"); // 只有 10 个库存
            int reduceQuantity = 20; // 想买 20 个

            // 2. Act & Assert (断言异常)
            // 我们期望它抛出 InvalidOperationException
            var exception = Assert.Throws<InvalidOperationException>(() =>
            {
                product.ReduceStock(reduceQuantity);
            });

            // 还可以检查报错信息对不对
            Assert.Equal("库存不足", exception.Message);
        }

        [Fact]
        public void CreateProduct_Should_Throw_Exception_When_Price_Is_Negative()
        {
            // 测试构造函数的守卫代码
            Assert.Throws<ArgumentException>(() =>
            {
                new Product("错误商品", -100m, 10, "描述");
            });
        }
    }
}