using System.ComponentModel.DataAnnotations; // 用于数据验证
/*
 为什么要用 DTO？

Entity (实体)：是厨房里的原材料（如：面粉、生肉），包含核心业务逻辑（如：扣库存逻辑），直接给客人吃（前端）不仅难看，还可能吃坏肚子（泄露隐私字段，如 CreatorId 或 IsDeleted）。

DTO (数据传输对象)：是摆盘好的菜肴（Menu Item），只包含客人想看的数据，或者客人下单时填写的表单。
 */
namespace EShop.Application.Products.Dtos
{
    public class CreateProductDto
    {
        [Required(ErrorMessage = "商品名称必填")]
        [StringLength(100, ErrorMessage = "名称不能超过100个字符")]
        public string Name { get; set; }

        [Range(0.01, double.MaxValue, ErrorMessage = "价格必须大于0")]
        public decimal Price { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "库存不能为负数")]
        public int Stock { get; set; }

        public string Description { get; set; }
    }

    public class ProductDto
    {
        public long Id { get; set; } // 前端需要ID来做详情查询
        public string Name { get; set; }
        public decimal Price { get; set; }
        public int Stock { get; set; }
        public string Description { get; set; }
        public DateTime CreationTime { get; set; } // 创建时间可以给前端看
    }
}