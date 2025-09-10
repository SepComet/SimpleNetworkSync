namespace Network;

public enum PacketType
{
    Data = 1,
    Ack = 2,
}

public struct Packet
{
    public PacketType Type;
    public byte[] Data;
    public uint SequenceNumber;

    /// <summary>
    /// 创建数据包
    /// </summary>
    /// <param name="data">包承载的字节数据载荷</param>
    /// <param name="sequenceNumber">包的序列号</param>
    /// <returns>由给定参数创建出来的 Packet</returns>
    public static Packet CreateDataPacket(byte[] data, uint sequenceNumber)
    {
        return new Packet()
        {
            Type = PacketType.Data,
            Data = data,
            SequenceNumber = sequenceNumber,
        };
    }

    /// <summary>
    /// 创建确认包
    /// </summary>
    /// <param name="sequenceNumber">确认包要确认的序列号</param>
    /// <returns>由给定参数创建出来的 Packet</returns>
    public static Packet CreateAckPacket(uint sequenceNumber)
    {
        return new Packet()
        {
            Type = PacketType.Ack,
            Data = [],
            SequenceNumber = sequenceNumber,
        };
    }

    /// <summary>
    /// 将一个 Packet 对象里的数据序列化为字节流，让它便于在网络中进行传输
    /// </summary>
    public byte[] ToBytes()
    {
        byte[] data = new byte[1 + 4 + Data.Length];
        
        data[0] = (byte)Type;
        BitConverter.GetBytes(SequenceNumber).CopyTo(data, 1);
        Data.CopyTo(data, 5);
        
        return data;
    }

    /// <summary>
    /// 将一串字节流序列化为 Packet，便于读取其中的数据
    /// </summary>
    /// <param name="data">需要序列化的字节流</param>
    public static Packet FromBytes(byte[] data)
    {
        return new Packet()
        {
            Type = (PacketType)data[0],
            SequenceNumber = BitConverter.ToUInt32(data, 1),
            Data = new ArraySegment<byte>(data, 5, data.Length - 5).ToArray()
        };
    }
}