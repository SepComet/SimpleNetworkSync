using System.Net;

namespace Network;

public interface ITransport
{
    void Send(byte[] data);
    void SendTo(byte[] data, IPEndPoint endPoint);
    event Action<byte[], IPEndPoint> OnReceive;
    Task StartAsync();
    void Stop();
}