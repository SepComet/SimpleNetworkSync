using Client;
using Network;
using System.Text;

namespace Program;

internal static class Program
{
    /// <summary>
    /// 客户端入口函数
    /// </summary>
    /// <remarks>
    /// <para>
    /// 执行流程
    /// </para>
    /// <list type="number">
    /// <item>检查命令行参数：args[0]:端口号 args[1]:服务端地址 args[2]:发送的消息</item>
    /// <item>若有 args[2] 参数，则向服务器发送消息，然后退出程序</item>
    /// <item>若没有 args[2] 参数，进入交互模式</item>
    /// <item>用户输入 quit 时退出</item>
    /// </list>
    /// </remarks>
    /// <param name="args">命令行参数（可选）</param>
    public static async Task Main1(string[] args)
    {
        int port = 8080;
        string address = "127.0.0.1";
        string message = "Hello";

        if (args.Length >= 1 && int.TryParse(args[0], out int p)) port = p;
        if (args.Length >= 2) address = args[1];
        if (args.Length >= 3) message = args[2];

        Console.WriteLine("UDP客户端启动");
        Console.WriteLine($"远程服务器：{address}:{port}");
        Console.WriteLine();

        var client = new SimpleUdpClient(address, port);
        bool receive = await client.SendMessageAsync(message);
        if (!receive)
        {
            Console.WriteLine("服务器连接失败");
            return;
        }

        if (args.Length >= 3)
        {
            await client.SendMessageAsync(message);
        }
        else
        {
            Console.WriteLine("服务器连接成功，开始交互模式");

            while (true)
            {
                Console.WriteLine("输入文本");
                message = Console.ReadLine();
                if (string.IsNullOrEmpty(message) || message == "quit")
                {
                    break;
                }
                await client.SendMessageAsync(message);
                Console.WriteLine();
            }
        }

        Console.WriteLine("客户端结束运行");
    }

    public static async Task Main(string[] args)
    {
        int port = args.Length >= 1 ? int.Parse(args[0]) : 8080;
        string address = args.Length >= 2 ? args[1] : "127.0.0.1";

        bool receiveMessage = false;

        var client = new ReliableUdpTransport(address, port);

        client.OnReceive += (data, endPoint) =>
        {
            string message = Encoding.UTF8.GetString(data);
            Console.WriteLine($"[Client] 应用层收到消息：‘{message}’");
            receiveMessage = true;
        };

        await client.StartAsync();

        // 发送测试信息
        Console.WriteLine("发送测试信息");
        client.Send(Encoding.UTF8.GetBytes("Hello"));
        Console.WriteLine("[Client] 应用层发送消息：‘Hello’");

        int waitTime = 0;
        while (!receiveMessage && waitTime < 10000)
        {
            await Task.Delay(100);
            waitTime += 100;
        }

        if (!receiveMessage)
        {
            Console.WriteLine("[Client] 接收超时");
            Console.WriteLine("[Client] 服务器连接失败");
            Console.WriteLine("按任意键退出...");
            Console.ReadKey();

            client.Stop();
            return;
        }

        Console.WriteLine("[Client] 连接成功，客户端运行中...");

        while (true)
        {
            string message = Console.ReadLine();
            if (!string.IsNullOrEmpty(message) && message != "quit")
            {

                var data = Encoding.UTF8.GetBytes(message);
                client.Send(data);
                Console.WriteLine($"[Client] 应用层发送消息：‘{message}’");
                Console.WriteLine();
            }
            else
            {
                break;
            }
        }

        Console.WriteLine("[Client] 应用层停止工作");
        client.Stop();
    }
}
