# ==========================================
# 阶段 1：基础运行时 (Base) - 极其轻量级的纯净服
# ==========================================
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
# 极其关键：.NET 8 默认安全监听 7200 端口
EXPOSE 7200

# ==========================================
# 阶段 2：极其暴力的构建工厂 (Build)
# ==========================================
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# 👇 1. 极其精准的依赖拷贝：只拷 csproj 文件，利用 Docker 缓存极速 Restore！
# 注意：因为我们是在 project 根目录执行打包，所以左边（宿主机）必须带上 EShop.Gateway/ 前缀
COPY ["EShop.Gateway/EShop.Gateway.csproj", "EShop.Gateway/"]

# 💡 架构师防御预警：如果你的 Identity 引用了 EventBus，必须把下面这行注释打开！
# COPY ["EShop.EventBus/EShop.EventBus.csproj", "EShop.EventBus/"]

# 2. 瞄准 Identity 项目进行包还原
RUN dotnet restore "EShop.Gateway/EShop.Gateway.csproj"

# 3. 依赖装完后，把解决方案下的所有源码全部拷进工厂
COPY . .

# 4. 极其核心的变阵：进入真正的 Identity 源码工作台！
WORKDIR "/src/EShop.Gateway"

# 5. 开始极其无情的编译
RUN dotnet build "EShop.Gateway.csproj" -c Release -o /app/build

# ==========================================
# 阶段 3：极其冷酷的裁剪发布 (Publish)
# ==========================================
FROM build AS publish
RUN dotnet publish "EShop.Gateway.csproj" -c Release -o /app/publish /p:UseAppHost=false

# ==========================================
# 阶段 4：极其纯净的最终产物 (Final)
# ==========================================
FROM base AS final
WORKDIR /app
# 极其丝滑的移花接木：把打包好的精简文件，拷到第一阶段的纯净服里！
COPY --from=publish /app/publish .

# 极其干脆的启动指令！
ENTRYPOINT ["dotnet", "EShop.Gateway.dll"]