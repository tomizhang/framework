# 一些注意项目
* consul host name 问题
    启动时候需携带 -node=127.0.0.1
    不然hont name会显示主机名称
    consul agent -dev -client=0.0.0.0  -ui -node=127.0.0.1