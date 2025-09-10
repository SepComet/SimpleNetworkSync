using System.Collections.Concurrent;
using System.Net;

namespace Network;

public class ClientSession
{
    public IPEndPoint EndPoint { get; }
    public DateTime LastActivityTime { get; private set; }

    // 发送相关
    public uint SendSequenceNumber { get; private set; } = 0;

    /// <summary>
    /// 线程安全的字典集合 ConcurrentDictionary，它是普通 Dictionary 的线程安全版本，专门设计用于多线程环境 
    /// </summary>
    /// <remarks>
    /// <para><b>特点</b>:
    /// <list type="bullet">
    /// <item>线程安全性：ConcurrentDictionary 内部使用了细粒度锁和无锁算法，允许多个线程同时安全地读取和修改集合，而不需要外部同步机制。</item>
    /// <item>高性能：相比使用传统锁保护的 Dictionary，ConcurrentDictionary 在高并发场景下性能更优，因为它减少了线程阻塞。</item>
    /// <item>使用场景：虽然 ConcurrentDictionary 是线程安全的，但在单线程场景下，普通的 Dictionary 性能会更好。因此建议只在确实需要多线程访问的场景下使用 ConcurrentDictionary。</item>
    /// </list>
    /// </para>
    /// </remarks>
    public ConcurrentDictionary<uint, (Packet packet, DateTime time)> PendingAcks { get; } = new();

    // 接收相关
    public uint ExpectedReceiveSequence { get; private set; } = 0;
    private HashSet<uint> _receivedSequences { get; } = new();

    private readonly object _lockObj = new();

    public ClientSession(IPEndPoint endPoint)
    {
        EndPoint = endPoint;
        LastActivityTime = DateTime.Now;
    }

    public bool TryProcessReceiveSequence(uint seqNum, out bool shouldDeliver)
    {
        lock (_lockObj)
        {
            LastActivityTime = DateTime.Now;

            if (seqNum == ExpectedReceiveSequence)
            {
                ExpectedReceiveSequence++;
                shouldDeliver = true;
                _receivedSequences.Add(seqNum);
                return true;
            }
            else if (seqNum > ExpectedReceiveSequence)
            {
                // 乱序到达，丢弃
                shouldDeliver = true;
                return _receivedSequences.Contains(seqNum);
            }
            else
            {
                shouldDeliver = false;
                return false;
            }
        }
    }

    public uint GetNextSequenceNumber()
    {
        lock (_lockObj)
        {
            return SendSequenceNumber++;
        }
    }
}