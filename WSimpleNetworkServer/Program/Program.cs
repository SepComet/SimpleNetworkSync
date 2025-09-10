using System.Text;
using Network;
using Server;

namespace Program;

internal static class Program
{
    /// <summary>
    /// 服务端入口函数（迭代一）
    /// </summary>
    /// <remarks>
    /// <para><b>执行流程</b>:
    /// <list type="number">
    /// <item>检查命令行参数，是否为给定的端口号</item>
    /// <item>设定 Ctrl+C 为关闭服务器指令</item>
    /// <item>实例化服务器对象并启动服务器</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="args">命令行参数（可选）</param>
    public static async Task Main1(string[] args)
    {
        SimpleUdpServer server;
        if (args.Length >= 1 && int.TryParse(args[0], out int port))
        {
            server = new SimpleUdpServer(port);
        }
        else
        {
            server = new SimpleUdpServer();
        }

        Console.CancelKeyPress += (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            server.Stop();
        };

        try
        {
            await server.StartAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"服务器启动错误：{ex.Message}");
        }

        Console.WriteLine("按任意键退出");
        Console.ReadKey();
    }

    /// <summary>
    /// 服务端入口函数（迭代二）
    /// </summary>
    /// <remarks>
    /// <para><b>执行流程</b>:
    /// <list type="number">
    /// <item>检查命令行参数，是否带有用户指定端口号，若没有则使用默认的 8080</item>
    /// <item>实例化服务器对象，为 OnReceive 事件添加自定义方法</item>
    /// <item>启动服务器</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <param name="args">命令行参数（可选）</param>
    public static async Task Main(string[] args)
    {
        int port = args.Length >= 1 ? int.Parse(args[0]) : 8080;

        var server = new ReliableUdpTransport(port);
        server.OnReceive += (data, senderEndPoint) =>
        {
            var message = Encoding.UTF8.GetString(data);
            if (message.Trim() == "Hello")
            {
                var response = Encoding.UTF8.GetBytes("World");
                server.SendTo(response, senderEndPoint);
                Console.WriteLine("[Server] 应用层发送回复：‘World’");
            }
            else
            {
                var response = Encoding.UTF8.GetBytes("Echo：" + message);
                server.SendTo(response, senderEndPoint);
                Console.WriteLine($"[Server] 应用层发送回复：‘Echo：{message}’");
            }
        };

        await server.StartAsync();

        Console.WriteLine("[Server] 服务端运行中，按任意键停止...");

        Console.ReadKey();
        server.Stop();
    }
}