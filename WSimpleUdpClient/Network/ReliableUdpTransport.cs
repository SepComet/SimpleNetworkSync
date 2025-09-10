using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace Network;

/// <summary>
/// 可靠 Udp 传输
/// </summary>
/// <remarks>
/// <para><b>发送流程</b>:
/// <list type="number">
/// <item>外部调用 Send(byte[]) 方法，将要传输的数据传入到对象中</item>
/// <item>Send(byte[])：创建数据包，并将其加入到等待确认字典中 _pendingAcks，再调用 SendPacket(Packet) 方法</item>
/// <item>SendPacket(Packet)：调用 SendPacketTo(Packet, IPEndPoint) 方法，根据当前是否为服务端为目标地址传入不同参数，服务端为 _lastRemoteEndPoint，客户端为 _defaultEndPoint</item>
/// <item>SendPacketTo(Packet, IPEndPoint)：序列化 Packet ，执行具体的 _client.SendAsync(byte[], IPEndPoint) 方法</item>
/// </list>
/// </para>
/// <para><b>接收流程</b>:
/// <list type="number">
/// <item>类对象实例化时启动 ReceiveLoop 方法的定时任务，每 100ms 执行一次</item>
/// <item>ReceiveLoop()：调用 _client.ReceiveAsync() 获取远端发送的数据并将其序列化为 Packet ，不同 Type 的 Packet 由不同的方法进行处理</item>
/// <item>HandleDataPacket(Packet)：处理数据包，向远端发送 Ack ，并检查该数据包是不是按序到达，若是则修改 _expectedSequenceNumber 等待处理下一个包，若提前则（丢弃、暂存），若为重复包则（丢弃）</item>
/// <item>HandleAckPacket(Packet)：处理 Ack 包，若该 Ack 存在于 _pendingAcks 中，即当前正在等待该 Ack ，将其移出等待确认字典，表示已经得到确认</item>
/// </list>
/// </para>
/// </remarks>
public class ReliableUdpTransport : ITransport
{
    private readonly UdpClient _client;
    private readonly IPEndPoint? _defaultEndPoint;
    private readonly bool _isServer;
    private ConcurrentDictionary<IPEndPoint, ClientSession> _sessions = new();

    /// <summary>
    /// 在指定时间间隔执行回调方法的轻量级计时器类 Timer
    /// </summary>
    /// <remarks>
    /// <para><b>特点</b>:
    /// <list type="bullet">
    /// <item>线程池执行：Timer 的回调方法在线程池线程上执行，不会阻塞主线程。</item>
    /// <item>垃圾回收问题：Timer 对象必须保持引用，否则可能被垃圾回收器回收导致定时器停止工作。</item>
    /// <item>异常处理：回调方法中的未处理异常会导致应用程序终止，务必添加适当的异常处理。</item>
    /// <item>资源释放：使用完毕后应调用 Dispose() 方法释放资源。</item>
    /// </list>
    /// </para>
    /// </remarks>
    private readonly Timer _retransmitTimer;

    private readonly Timer _cleanupTimer;

    /// <summary>
    /// volatile 关键字，它告诉编译器和运行时系统该字段可能被多个线程同时访问，从而禁用某些优化并确保内存可见性
    /// </summary>
    /// <remarks>
    /// <para><b>特点</b>:
    /// <list type="bullet">
    /// <item>基本概念：在多线程环境中，编译器和 CPU 可能会对代码进行优化，包括指令重排序、寄存器缓存等。volatile 关键字防止这些优化对特定字段产生不利影响。</item>
    /// <item>内存可见性保证：读取总是获取最新的值，不会使用缓存的旧值；写入会立即刷新到主内存，其他线程能立即看到更改。</item>
    /// <item>适用场景：用于线程间通信的简单布尔标志。</item>
    /// <item>不保证原子性：volatile 只保证可见性，不保证复合操作的原子性。</item>
    /// </list>
    /// </para>
    /// </remarks>
    private volatile bool _isRunning;

    // 配置参数
    private const int RetransmitTimeoutMs = 1000;
    private const int MaxRetransmitAttempts = 5;
    private const int CleanupTimeoutMs = 30000;

    public event Action<byte[], IPEndPoint>? OnReceive;

    /// <summary>
    /// 服务端构造函数
    /// </summary>
    public ReliableUdpTransport(int port)
    {
        _client = new UdpClient(port);
        _isServer = true;
        _retransmitTimer = new Timer(CheckRetransmit, null, 100, 100);
        _cleanupTimer = new Timer(CleanupSession, null, 100, 100);
        Console.WriteLine($"[Transport] 服务端模式，监听窗口：{port}");
    }

    /// <summary>
    /// 客户端构造函数
    /// </summary>
    public ReliableUdpTransport(string ip, int port)
    {
        _client = new UdpClient(0);
        _defaultEndPoint = new IPEndPoint(IPAddress.Parse(ip), port);

        _isServer = false;
        _retransmitTimer = new Timer(CheckRetransmit, null, 100, 100);
        _cleanupTimer = new Timer(CleanupSession, null, 100, 100);
        Console.WriteLine($"[Transport] 客户端模式，目标：{_defaultEndPoint}");
    }

    public async Task StartAsync()
    {
        _isRunning = true;
        _sessions.Clear();
        Console.WriteLine("[Transport] 运输层启动");

        _ = Task.Run(ReceiveLoop);
        await Task.Delay(100);
    }

    public void Stop()
    {
        _isRunning = false;
        _retransmitTimer.Dispose();
        _cleanupTimer.Dispose();
        _client.Close();
        _sessions.Clear();
        Console.WriteLine("[Transport] 运输层停止");
    }

    public void Send(byte[] data)
    {
        if (!_isServer && _defaultEndPoint != null)
        {
            uint seqNum = GetOrCreateSession(_defaultEndPoint).GetNextSequenceNumber();
            var packet = Packet.CreateDataPacket(data, seqNum);
            SendPacketTo(packet, _defaultEndPoint);
        }
    }

    public void SendTo(byte[] data, IPEndPoint endPoint)
    {
        if (!_isRunning)
        {
            return;
        }

        var session = GetOrCreateSession(endPoint);
        uint seqNum = session.GetNextSequenceNumber();
        var packet = Packet.CreateDataPacket(data, seqNum);
        
        session.PendingAcks[seqNum] = (packet, DateTime.Now);
        
        SendPacketTo(packet, endPoint);
        Console.WriteLine($"[Transport] 发送数据包到 {endPoint} SeqNum={seqNum}，DataLen={data.Length}");
    }

    private async void SendPacketTo(Packet packet, IPEndPoint? endPoint)
    {
        try
        {
            var bytes = packet.ToBytes();
            await _client.SendAsync(bytes, bytes.Length, endPoint);
        }
        catch (Exception e)
        {
            Console.WriteLine($"[Transport] 发送错误：{e.Message}");
        }
    }

    private async void ReceiveLoop()
    {
        while (_isRunning)
        {
            try
            {
                var result = await _client.ReceiveAsync();
                var packet = Packet.FromBytes(result.Buffer);

                if (packet.Type == PacketType.Data)
                {
                    HandleDataPacket(packet, result.RemoteEndPoint);
                }
                else if (packet.Type == PacketType.Ack)
                {
                    HandleAckPacket(packet, result.RemoteEndPoint);
                }
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception e)
            {
                Console.WriteLine($"[Transport] 接受错误：{e.Message}");
            }
        }
    }

    private void HandleDataPacket(Packet packet, IPEndPoint senderEndPoint)
    {
        var ackPacket = Packet.CreateAckPacket(packet.SequenceNumber);
        SendPacketTo(ackPacket, senderEndPoint);
        Console.WriteLine($"[Transport] 发送ACK SeqNum={packet.SequenceNumber}");

        var session = GetOrCreateSession(senderEndPoint);
        if (session.TryProcessReceiveSequence(packet.SequenceNumber, out bool shouldDeliver))
        {
            if (shouldDeliver)
            {
                OnReceive?.Invoke(packet.Data, senderEndPoint);
                Console.WriteLine($"[Transport] 交付数据包从 {senderEndPoint}：SeqNum={packet.SequenceNumber}");
            }
            else
            {
                Console.WriteLine($"[Transport] 收到重复包从 {senderEndPoint}：SeqNum={packet.SequenceNumber}，忽略");
            }
        }
        else
        {
            Console.WriteLine($"[Transport] 收到乱序包从 {senderEndPoint}：SeqNum={packet.SequenceNumber}，丢弃");
        }
    }

    private void HandleAckPacket(Packet packet, IPEndPoint senderEndPoint)
    {
        Console.WriteLine($"[Transport] 收到ACK从 {senderEndPoint} SeqNum={packet.SequenceNumber}");
        var session = GetOrCreateSession(senderEndPoint);

        if (session.PendingAcks.TryRemove(packet.SequenceNumber, out _))
        {
            Console.WriteLine($"[Transport] 确认包到 {senderEndPoint} SeqNum={packet.SequenceNumber}");
        }
    }

    private void CheckRetransmit(object? state)
    {
        if (!_isRunning)
        {
            return;
        }

        var now = DateTime.Now;
        List<(uint seqNum, Packet packet, IPEndPoint endPoint)> needRetransmits = new();

        foreach (var sessionKvp in _sessions)
        {
            var session = sessionKvp.Value;
            foreach (var packetKvp in session.PendingAcks)
            {
                if ((now - packetKvp.Value.time).TotalMilliseconds > RetransmitTimeoutMs)
                {
                    needRetransmits.Add((packetKvp.Key, packetKvp.Value.packet, sessionKvp.Key));
                }
            }
        }


        foreach (var tuple in needRetransmits)
        {
            SendPacketTo(tuple.packet, tuple.endPoint);
            Console.WriteLine($"[Transport] 重传包 SeqNum={tuple.seqNum}");
        }
    }

    private void CleanupSession(object? state)
    {
        if (!_isRunning)
        {
            return;
        }
        
        var now = DateTime.Now;
        var toRemove = new List<IPEndPoint>();

        foreach (var sessionKvp in _sessions)
        {
            var session = sessionKvp.Value;
            if ((now - session.LastActivityTime).TotalMilliseconds > CleanupTimeoutMs)
            {
                toRemove.Add(sessionKvp.Key);
            }
        }

        foreach (var endPoint in toRemove)
        {
            if (_sessions.TryRemove(endPoint, out var session))
            {
                Console.WriteLine($"[Transport] 客户端 {endPoint} 长时间未响应，已结束会话");
            }
        }

        if (_isServer)
        {
            PrintSessionInfo();
        }
    }

    private ClientSession GetOrCreateSession(IPEndPoint endPoint)
    {
        return _sessions.GetOrAdd(endPoint, _ =>
        {
            var session = new ClientSession(endPoint);
            Console.WriteLine("创建新对话");
            return session;
        });
    }
    
    private void PrintSessionInfo()
    {
        Console.WriteLine($"当前活跃会话数：{_sessions.Count}");
        foreach (var sessionKvp in _sessions)
        {
            var session = sessionKvp.Value;
            Console.WriteLine(
                $"  会话：{session.EndPoint}，发送SeqNum：{session.SendSequenceNumber}，期望接收：{session.ExpectedReceiveSequence}，待确认: {session.PendingAcks.Count}");
        }
    }
}