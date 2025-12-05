namespace EShop.Application.Orders.Dtos
{
    public class OrderCreatedEvent
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
        public long UserId { get; set; } // 谁买的
        public DateTime OrderTime { get; set; }
    }
}