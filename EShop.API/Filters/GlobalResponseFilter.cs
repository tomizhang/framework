using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Diagnostics;

namespace EShop.API.Filters
{
    // 极其优雅的过滤器：专门负责拦截所有 Controller 返回的结果并进行包装
    public class GlobalResponseFilter : IAsyncResultFilter
    {
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            // 1. 极其硬核的联动：把我们刚刚做好的 TraceId 提取出来！
            var traceId = Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

            // 2. 检查控制器返回的到底是什么类型的结果
            if (context.Result is ObjectResult objectResult)
            {
                var statusCode = objectResult.StatusCode ?? context.HttpContext.Response.StatusCode;

                // 组装大厂标准格式的匿名对象 (不需要额外建类，极其轻量)
                var apiResponse = new
                {
                    code = statusCode,
                    message = statusCode >= 400 ? "请求发生错误" : "操作成功",
                    data = objectResult.Value, // 把原本要返回的真实数据塞进 data 里！
                    traceId = traceId
                };

                // 强行替换掉原本裸奔的返回值，换成我们的精美包装盒！
                context.Result = new ObjectResult(apiResponse)
                {
                    StatusCode = statusCode
                };
            }
            else if (context.Result is EmptyResult)
            {
                // 3. 如果你的接口写的是 return Ok(); (里面没传数据)，给它个空包装
                var apiResponse = new
                {
                    code = 200,
                    message = "操作成功",
                    data = (object)null,
                    traceId = traceId
                };
                context.Result = new ObjectResult(apiResponse) { StatusCode = 200 };
            }

            // 4. 打包完毕，放行！发送给前端！
            await next();
        }
    }
}