using System.Diagnostics;

namespace EShop.API.Middlewares
{
    public class CommonMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public CommonMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.Response.OnStarting(() =>
            {
                // 提取标准的分布式追踪 ID
                var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

                // 悄悄塞进 HTTP Response Header 里
                context.Response.Headers["X-Trace-Id"] = traceId;
                // 👇 极其硬核：把当前处理这个请求的“服务器物理真身”直接暴露给前端！
                context.Response.Headers["X-Server-Node"] = Environment.MachineName;

                return Task.CompletedTask;
            });
            await _next(context);

        }

    }
}
