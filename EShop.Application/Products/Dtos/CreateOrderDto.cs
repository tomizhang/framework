namespace EShop.Application.Orders.Dtos
{
    public class CreateOrderDto
    {
        public long ProductId { get; set; }
        public int Quantity { get; set; }
    }
}