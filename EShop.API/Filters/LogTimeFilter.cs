using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics; // 也就是秒表

namespace EShop.API.Filters
{
    public class LogTimeFilter : IAsyncActionFilter
    {
        private readonly ILogger<LogTimeFilter> _logger;

        public LogTimeFilter(ILogger<LogTimeFilter> logger)
        {
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            // 🟢 1. 接口执行前：按下秒表
            var stopwatch = Stopwatch.StartNew();
            var actionName = context.ActionDescriptor.DisplayName;

            // 让请求继续跑...
            var resultContext = await next();

            // 🔴 2. 接口执行后：停止秒表
            stopwatch.Stop();
            var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

            if (elapsedMilliseconds > 500) // 阈值：如果超过 500ms 就算慢接口
            {
                _logger.LogWarning($"[慢请求警告] 接口 {actionName} 耗时: {elapsedMilliseconds}ms");
            }
            else
            {
                _logger.LogInformation($"[性能监控] 接口 {actionName} 耗时: {elapsedMilliseconds}ms");
            }
        }
    }
}