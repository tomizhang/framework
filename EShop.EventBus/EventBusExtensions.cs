using Microsoft.Extensions.DependencyInjection;

namespace EShop.EventBus
{
    // 极其标准的 .NET 扩展方法写法
    public static class EventBusExtensions
    {
        // 这个 this IServiceCollection 就是魔法的源泉！
        public static IServiceCollection AddRabbitMqEventBus(this IServiceCollection services, string hostName = "localhost")
        {
            // 在组件内部，替业务微服务把枪装配好！
            services.AddScoped<IMessageProducer>(sp =>
            {
                return new RabbitMqProducer(hostName);
            });

            return services;
        }
    }
}