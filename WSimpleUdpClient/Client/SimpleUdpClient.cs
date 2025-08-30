using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Client;

public class SimpleUdpClient
{
    public async Task SendMessageAsync(int port, string serverIP, string message)
    {
        using (UdpClient client = new UdpClient())
        {
            try
            {
                client.Client.ReceiveTimeout = 5000;

                IPEndPoint serverIPEnd = new IPEndPoint(IPAddress.Parse(serverIP), port);

                byte[] buffer = Encoding.UTF8.GetBytes(message);
                await client.SendAsync(buffer, buffer.Length, serverIPEnd);
                Console.WriteLine($"[{DateTime.Now::HH:mm:ss}] 发送消息：‘{message}’ -> {serverIPEnd}");

                UdpReceiveResult result = await client.ReceiveAsync();
                string response = Encoding.UTF8.GetString(result.Buffer);
                Console.WriteLine($"[{DateTime.Now::HH:mm:ss}] 收到消息：‘{response}’ <- {result.RemoteEndPoint}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"客户端错误：{ex.Message}");
            }
        }
    }
}
