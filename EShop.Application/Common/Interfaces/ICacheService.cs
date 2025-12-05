namespace EShop.Application.Common.Interfaces
{
    public interface ICacheService
    {
        // 泛型方法：T 可以是任何类型 (ProductDto, int, string 等)
        // 获取缓存
        Task<T?> GetAsync<T>(string key);

        // 设置缓存 (带过期时间)
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null);

        // 移除缓存
        Task RemoveAsync(string key);
    }
}