var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

// 开启静态文件支持 (让它能读取 wwwroot 里的 html)
app.UseStaticFiles();


app.Run();
