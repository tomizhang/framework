using EShop.Domain.Common;

namespace EShop.Domain.Entities
{
    public class Order : FullAuditedEntity<long>
    {
        public long ProductId { get; private set; }
        public int Quantity { get; private set; }
        public long UserId { get; private set; }
        public string Status { get; private set; } // "Pending", "Completed"

        private Order() { }

        public Order(long productId, int quantity, long userId)
        {
            ProductId = productId;
            Quantity = quantity;
            UserId = userId;
            Status = "Pending"; // 刚创建是待处理
        }

        public void MarkAsCompleted()
        {
            Status = "Completed";
        }
    }
}