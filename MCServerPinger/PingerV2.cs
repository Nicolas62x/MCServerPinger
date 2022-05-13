
using System.Buffers;
using System.Net;
using System.Net.Sockets;
namespace MinecraftPinger;
public static class PingerV2
{
    static ArrayPool<byte> pool = ArrayPool<byte>.Shared;
    static readonly byte[] mcRq = new byte[] { 6, 0, 0, 0, 0x63, 0xdd, 1, 1, 0 };

    public delegate void PingCallback(string motd, EndPoint ep);
    public delegate void FaillCallback(bool didConnect, EndPoint ep);

    static PriorityQueue<Socket, DateTime> to = new PriorityQueue<Socket, DateTime>();
    static object locker = new object();

    static Queue<byte[]> bufs = new Queue<byte[]>(5000);
    static object locker2 = new object();


    static PingerV2()
    {
        new Thread(TimeOuter).Start();

        for (int i = 0; i < 5000; i++)
        {
            bufs.Enqueue(new byte[5000]);
        }
    }

    static void TimeOuter()
    {
        while (true)
        {
            DateTime next = DateTime.MinValue;
            Socket? s = null;

            lock (locker)
                if (to.Count > 0)
                    to.TryPeek(out s, out next);

            if (s is not null && next < DateTime.Now)
                lock (locker)
                    to.Dequeue().Dispose();
            else
                Thread.Sleep(5);
        }
    }

    public static async Task Ping(EndPoint ep, PingCallback sucess, FaillCallback faill, int timeout = 10)
    {
        using Socket s = new Socket(SocketType.Stream, ProtocolType.Tcp);
        s.LingerState = new LingerOption(false, 0);
        s.NoDelay = true;

        lock (locker)
            to.Enqueue(s, DateTime.Now.AddSeconds(timeout));

        byte[]? buf = null;
        bool connect = false;
        try
        {
            await s.ConnectAsync(ep).ConfigureAwait(false);
        
            if (!s.Connected)
                return;

            connect = true;

            int sent = await s.SendAsync(mcRq, SocketFlags.None).ConfigureAwait(false);

            if (sent != mcRq.Length)
                return;

            rst:

            lock (locker2)
                if (bufs.Count > 0)
                    buf = bufs.Dequeue();

            if (buf is null)
            {
                Thread.Sleep(1);
                Console.WriteLine("ERROR");
                goto rst;
            }

            int len = 0;
            int ptr = 0;
            int retry = 0;
            VARINT v = new VARINT();
        
            relisten:

            if (retry++ > 10)
                throw new Exception("To many retry");

            len += await s.ReceiveAsync(new ArraySegment<byte>(buf, len, buf.Length - len), SocketFlags.None).ConfigureAwait(false);

            while (!v.finished && ptr < len)
                v += buf[ptr++];

            if (!v.finished || v.value + ptr > len)
                goto relisten;

            if (v.value <= 1)
                throw new Exception("Invalid Len");

            if (buf[ptr++] != 0)
                throw new Exception("Invalid ID");

           
            sucess(STRING.readString(new ArraySegment<byte>(buf, 0, len), ref ptr), ep);

        }
        catch (Exception)
        {
            try
            {
                faill(connect, ep);
            }
            catch (Exception)
            {
            }

            throw;
        }
        finally
        {
            if (buf is not null)
                lock (locker2)
                    bufs.Enqueue(buf);
        }

    }
}
