namespace EShop.Application.Common.Interfaces
{
    public interface IMessageProducer
    {
        // T 可是是任何对象，比如 OrderDto
        void SendMessage<T>(T message);
    }
}