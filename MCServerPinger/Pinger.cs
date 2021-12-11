
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MinecraftPinger;
public class Pinger
{
    Socket? s;
    byte[] buf;
    public EndPoint? ep;

    public delegate void PingCallback(string motd, Pinger pinger);
    public delegate void FaillCallback(bool didConnect, Pinger pinger);

    PingCallback onok;
    FaillCallback onfaill;

    int lastlen;
    bool didConnect;

    static ArrayPool<byte> pool = ArrayPool<byte>.Shared;

    public Pinger(PingCallback cb, FaillCallback cb2)
    {
        this.onok = cb;
        this.onfaill = cb2;
        this.buf = pool.Rent(32000);
    }

    ~Pinger() => pool.Return(buf);

    public void StartChecking(IPEndPoint endPoint)
    {
        if (s is not null)
            throw new Exception("Previous connection was not finished");

        ep = endPoint;

        s = new Socket(SocketType.Stream, ProtocolType.Tcp);

        s.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Linger, false);
        s.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
        
        didConnect = false;

        try
        {
            s.BeginConnect(endPoint, OnCo, this);
        }
        catch (Exception)
        {
            s.Dispose();
            s = null;
        }
        
    }

    public void Stop() => s?.Close();

    static readonly byte[] mcRq = new byte[] { 6, 0, 0, 0, 0x63, 0xdd, 1, 1, 0 };

    static void OnCo(IAsyncResult res)
    {
        Pinger? pinger = (Pinger?)res.AsyncState;

        if (pinger is null)
            throw new ArgumentException("Pinger should not be null");
        if (pinger.s is null)
            throw new ArgumentException("Socket should not be null");

        try
        {
            pinger.s.EndConnect(res);
            pinger.didConnect = true;

            pinger.s.BeginSend(mcRq, 0, mcRq.Length, SocketFlags.None, null, null);
            pinger.lastlen = 0;
            pinger.s.BeginReceive(pinger.buf, 0, pinger.buf.Length, SocketFlags.None, OnRCV, pinger);
        }
        catch (Exception)
        {
            pinger.s.Dispose();
            pinger.s = null;

            try
            {
                pinger.onfaill(pinger.didConnect, pinger);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }
    }

    static void OnRCV(IAsyncResult res)
    {
        Pinger? pinger = (Pinger?)res.AsyncState;

        if (pinger is null)
            throw new ArgumentException("Pinger should not be null");
        if (pinger.s is null)
            throw new ArgumentException("Socket should not be null");
        if (pinger.buf is null)
            throw new ArgumentException("Buffer should not be null");

        try
        {
            int len = pinger.s.EndReceive(res);

            pinger.lastlen += len;
            len = pinger.lastlen;

            if (len == 0)
                throw new Exception("No data received");
       
            int ptr = 0;

            VARINT v = new VARINT();

            do
            {
                v += pinger.buf[ptr++];
            }
            while (!v.finished && ptr < len);

            if (!v.finished || v.value + ptr > len)
            {
                pinger.s.BeginReceive(pinger.buf, pinger.lastlen, pinger.buf.Length - pinger.lastlen, SocketFlags.None, OnRCV, pinger);
                return;
            }

            if (pinger.buf[ptr++] != 0)
                throw new Exception("Invalid ID");

            v = new VARINT();

            do
            {
                v += pinger.buf[ptr++];
            }
            while (!v.finished && ptr < len);

            if (!v.finished || v.value + ptr > len)
            {
                pinger.s.BeginReceive(pinger.buf, pinger.lastlen, pinger.buf.Length - pinger.lastlen, SocketFlags.None, OnRCV, pinger);
                return;
            }

            string motd = Encoding.UTF8.GetString(pinger.buf, ptr, v.value);

            pool.Return(pinger.buf);
            pinger.s.Dispose();

            try
            {
                pinger.onok(motd, pinger);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            
        }
        catch (Exception)
        {
            pinger.s.Dispose();           

            pinger.onfaill(pinger.didConnect, pinger);
        }

        pinger.s = null;
    }
}
