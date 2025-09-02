using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client;

public class SimpleUdpClient
{
    private readonly UdpClient _client;
    private readonly IPEndPoint _endPoint;

    public SimpleUdpClient(string ip, int port)
    {
        _client = new UdpClient(0);
        _client.Client.ReceiveTimeout = 5000;
        _endPoint = new IPEndPoint(IPAddress.Parse(ip), port);
    }

    public async Task<bool> SendMessageAsync(string message)
    {
        try
        {
            byte[] buffer = Encoding.UTF8.GetBytes(message);
            await _client.SendAsync(buffer, buffer.Length, _endPoint);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 发送消息：‘{message}’ -> {_endPoint}");

            UdpReceiveResult result = await _client.ReceiveAsync();
            string response = Encoding.UTF8.GetString(result.Buffer);
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] 收到消息：‘{response}’ <- {result.RemoteEndPoint}");

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"客户端错误：{ex.Message}");
            return false;
        }
    }
}
