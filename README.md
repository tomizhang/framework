# 一些注意项目
* 分布式日志追踪
  localhost:16686
  docker run -d --name jaeger \
  -p 16686:16686 \
  -p 4317:4317 \
  jaegertracing/all-in-one:latest
* 日志搜索 
   localhost:5341
   docker run -d --name seq   -e ACCEPT_EULA=Y   -e SEQ_FIRSTRUN_ADMINPASSWORD="Password123!"   -p 5341:80 datalust/seq:latest
* consul host name 问题
    启动时候需携带 -node=127.0.0.1
    不然hont name会显示主机名称
    consul agent -dev -client=0.0.0.0  -ui -node=127.0.0.1

# 其它
 告诉 EF Core：我加了一张叫 RolePermissions 的表！
dotnet ef migrations add AddRolePermissionsTable

 把这波修改（包含那条 Admin 的救命数据）直接刷进 eshop.db 文件里！
dotnet ef database update
 1. 明确指定到具体的 .csproj 文件！
dotnet ef migrations add AddRolePermissionsTable --project EShop.Infrastructure/EShop.Infrastructure.csproj --startup-project EShop.API/EShop.API.csproj
 2. 刷入数据库
dotnet ef database update --project EShop.Infrastructure/EShop.Infrastructure.csproj --startup-project EShop.API/EShop.API.csproj
 3. --verbose 显示调试信息
dotnet ef database update --project EShop.Infrastructure --startup-project EShop.API --verbose
 4. 注意踩坑点
如果新迁移文件 up方法没有即为空白时候 up(){ nothing。。。} 即为这次迁移和上次比较并没有差别所以无变化，
解决方式:删除所有迁移文件或者删除上次迁移修改部分
