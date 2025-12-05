using System.Net;
using System.Text.Json;

namespace EShop.API.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                // 🟢 1. 放行：让请求继续往下走 (去 Controller, Service...)
                await _next(context);
            }
            catch (Exception ex)
            {
                // 🔴 2. 捕获：如果下面任何地方报错了，这里都能抓到
                _logger.LogError(ex, "全局异常捕获: {Message}", ex.Message);

                // 3. 处理：返回一个漂亮的 JSON 给前端，而不是 500 网页
                await HandleExceptionAsync(context, ex);
            }
        }

        private static Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            // 根据异常类型决定状态码 (这里简单统一为 500，也可以根据业务异常细分)
            context.Response.StatusCode = (int)HttpStatusCode.InternalServerError;

            var response = new
            {
                StatusCode = context.Response.StatusCode,
                Message = "服务器开小差了，请联系管理员", // 生产环境不要把 ex.Message 直接给前端，不安全
                DetailedError = exception.Message // 开发环境可以把这个加上方便调试
            };

            var json = JsonSerializer.Serialize(response);
            return context.Response.WriteAsync(json);
        }
    }
}