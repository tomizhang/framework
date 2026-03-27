using Microsoft.AspNetCore.SignalR;

namespace EShop.MessageCenter.Hubs
{
    // 全站唯一的前端长连接终点！
    public class NotificationHub : Hub
    {
        public override async Task OnConnectedAsync()
        {
            Console.WriteLine($"[雷达响应] 前端设备已连接！分配信道ID: {Context.ConnectionId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            Console.WriteLine($"[雷达响应] 前端设备断开连接！信道ID: {Context.ConnectionId}");
            await base.OnDisconnectedAsync(exception);
        }
    }
}