using Consul;

namespace EShop.API
{
    public static class ConsulBuilderExtensions
    {
        public static IApplicationBuilder RegisterConsul(this IApplicationBuilder app, IConfiguration configuration, IHostApplicationLifetime lifetime)
        {
            // 1. 创建 Consul 客户端 (连到我们的本地 8500 端口)
            var consulClient = new ConsulClient(c =>
            {
                c.Address = new Uri("http://localhost:8500");
            });

            // 2. 获取当前服务的地址和端口 (假设 API 跑在 5002 端口)
            // 实际生产中可以从 configuration 读取
            var serviceIp = "127.0.0.1";
            var servicePort = 7001;
            var serviceName = "EShop-API"; // 👈 这个名字极其重要！网关就靠它找人了
            var serviceId = $"{serviceName}-{Guid.NewGuid()}";

            // 3. 配置注册信息和健康检查
            var registration = new AgentServiceRegistration()
            {
                ID = serviceId,
                Name = serviceName,
                Address = serviceIp,
                Port = servicePort,
                Check = new AgentServiceCheck()
                {
                    // 告诉 Consul 每隔 10 秒来敲一次门，看看我活着没
                    DeregisterCriticalServiceAfter = TimeSpan.FromSeconds(5),
                    Interval = TimeSpan.FromSeconds(10),
                    HTTP = $"http://{serviceIp}:{servicePort}/health", // 需要在 API 里加个健康检查接口
                    Timeout = TimeSpan.FromSeconds(5)
                }
            };

            // 4. 在程序启动时注册，在程序停止时注销
            lifetime.ApplicationStarted.Register(() =>
            {
                consulClient.Agent.ServiceRegister(registration).Wait();
                Console.WriteLine($"[Consul] 服务 {serviceName} 已成功注册！");
            });

            lifetime.ApplicationStopping.Register(() =>
            {
                consulClient.Agent.ServiceDeregister(serviceId).Wait();
                Console.WriteLine($"[Consul] 服务 {serviceName} 已注销！");
            });

            return app;
        }
    }
}