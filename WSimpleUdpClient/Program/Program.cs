using Client;
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
    public static async Task Main(string[] args)
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
}
