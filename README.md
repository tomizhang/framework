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