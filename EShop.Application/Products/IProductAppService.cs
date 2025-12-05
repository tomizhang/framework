using EShop.Application.Products.Dtos;

namespace EShop.Application.Products
{
    public interface IProductAppService
    {
        // 这里的返回值 Task<ProductDto> 意味着我们在做异步编程
        Task<ProductDto> CreateAsync(CreateProductDto input);

        // 以后还可以加 GetAsync, UpdateAsync 等
        Task<ProductDto> GetAsync(long id);
        Task<bool> ReduceStockAsync(long id, int quantity);
        Task PlaceOrderAsync(long productId, int quantity, long userId);
    }
}