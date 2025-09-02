using System.Net.Sockets;
using System.Text;

namespace Server;

public class SimpleUdpServer(int _port = 8080)
{
    private UdpClient? _client;
    private bool _running;

    /// <summary>
    /// 启动监听
    /// </summary>
    /// <remarks>
    /// <list type="number">
    /// <item>根据端口号创建客户 UdpClient 实例</item>
    /// <item>开始死循环通过 UdpClient.ReceiverAsync() 接受客户消息</item>
    /// <item>通过 Encoding.UTF8.GetString(buffer) 将字节流转为字符串</item>
    /// <item>通过 UdpClient.SendAsync(buffer, buffer.Length, RemoteEndPoint) 来回复消息</item>
    /// </list>
    /// </remarks>
    public async Task StartAsync()
    {
        _client = new UdpClient(_port);
        _running = true;

        Console.WriteLine($"UDP服务启动，正在监听 {_port} 端口");
        Console.WriteLine("等待客户端连接");
        Console.WriteLine("按 Ctrl+C 以停止服务器监听");

        while (_running)
        {
            try
            {
                var result = await _client.ReceiveAsync();
                string message = Encoding.UTF8.GetString(result.Buffer);

                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到消息：‘{message}’ 来自{result.RemoteEndPoint}");

                string response;
                if (message.Trim() == "Hello")
                {
                    response = "World";
                }
                else
                {
                    response = "Echo: " + message;
                }

                byte[] buffer = Encoding.UTF8.GetBytes(response);
                await _client.SendAsync(buffer, buffer.Length, result.RemoteEndPoint);
                Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 回复消息：‘{response}’ 发往{result.RemoteEndPoint}");
            }
            catch (ObjectDisposedException)
            {
                Console.WriteLine("远程客户端已关闭");
                break;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"服务器异常：{ex.Message}");
            }
        }
    }

    public void Stop()
    {
        _running = false;
        _client?.Close();
        Console.WriteLine("服务端已停止");
    }
}