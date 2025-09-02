using System.Text;
using Server;

namespace Program;

internal static class Program
{
    /// <summary>
    /// 服务端入口函数
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
    public static async Task Main(string[] args)
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
}