
using System.Buffers;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace MinecraftPinger;
public class Pinger
{
    Socket? s;
    byte[] buf;
    public IPEndPoint? ep;
    public bool available;

    public delegate void PingCallback(string motd, Pinger pinger);
    public delegate void FaillCallback(bool didConnect, Pinger pinger);

    PingCallback onok;
    FaillCallback onfaill;

    int lastlen;
    bool didConnect;

    int retry;

    static ArrayPool<byte> pool = ArrayPool<byte>.Shared;

    static PriorityQueue<Socket, DateTime> sockets = new PriorityQueue<Socket, DateTime>();
    static object socketLock = new object();
    static Task disposer = Task.Run(() =>
    {
        while (true)
        {
            try
            {
                Socket? s = null;
                DateTime dt = DateTime.MinValue;
                lock (socketLock)
                    if (sockets.Count > 0)
                        sockets.TryDequeue(out s, out dt);

                while (dt > DateTime.Now)
                    Thread.Sleep(5);

                if (s is not null)
                    s.Dispose();

            }
            catch (Exception)
            {

            }
        }
    });

    public Pinger(PingCallback success, FaillCallback fail)
    {
        this.onok = success;
        this.onfaill = fail;
        this.buf = pool.Rent(32_768);
        available = true;
    }

    ~Pinger() => pool.Return(buf);

    public void Get(IPEndPoint endPoint, int timeout = 10)
    {
        if (s is not null)
            throw new Exception("Previous connection was not finished");

        ep = endPoint;

        Get(timeout);
    }
    public void Get(IPAddress addr, int timeout = 10)
    {
        if (s is not null)
            throw new Exception("Previous connection was not finished");

        if (ep is null)
            ep = new IPEndPoint(addr, 25565);
        else
            ep.Address = addr;

        Get(timeout);
    }
    void Get(int timeout)
    {
        available = false;
        s = new Socket(SocketType.Stream, ProtocolType.Tcp);

        lock (socketLock)
            sockets.Enqueue(s, DateTime.Now.AddSeconds(timeout));

        
        s.LingerState = new LingerOption(false, 0);
        s.NoDelay = true;
        
        didConnect = false;

        try
        {
            retry = 0;
            s.BeginConnect(ep, OnCo, this);
        }
        catch (Exception)
        {
            s.Dispose();
            s = null;
            available = true;
        }
        
    }

    public void Stop() => s?.Close();

    static readonly byte[] mcRq = new byte[] { 6, 0, 0, 0, 0x63, 0xdd, 1, 1, 0 };

    static void OnCo(IAsyncResult res)
    {
        Pinger? pinger = (Pinger?)res.AsyncState;

        if (pinger is null)
            return;
        if (pinger.s is null)
            return;

        try
        {
            pinger.s.EndConnect(res);
            pinger.didConnect = true;

            pinger.s.BeginSend(mcRq, 0, mcRq.Length, SocketFlags.None, OnSND, pinger);            
        }
        catch (Exception)
        {
            pinger.s.Dispose();

            try
            {
                pinger.onfaill(pinger.didConnect, pinger);
            }
            catch (Exception)
            {
            }

            pinger.s = null;
            pinger.available = true;
        }
    }

    static void OnSND(IAsyncResult res)
    {
        Pinger? pinger = (Pinger?)res.AsyncState;

        if (pinger is null)
            return;
        if (pinger.s is null)
            return;
        if (pinger.buf is null)
            return;

        try
        {
            if (pinger.s.EndSend(res) != mcRq.Length)
                throw new Exception();

            pinger.lastlen = 0;
            pinger.s.BeginReceive(pinger.buf, 0, pinger.buf.Length, SocketFlags.None, OnRCV, pinger);

        }
        catch (Exception)
        {
            pinger.s.Dispose();

            try
            {
                pinger.onfaill(pinger.didConnect, pinger);
            }
            catch (Exception)
            {

            }
            
            pinger.s = null;
            pinger.available = true;
        }
    }

    static void OnRCV(IAsyncResult res)
    {
        Pinger? pinger = (Pinger?)res.AsyncState;

        if (pinger is null)
            return;
        if (pinger.s is null)
            return;
        if (pinger.buf is null)
            return;

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
                pinger.retry++;
                if (pinger.retry > 10)
                    throw new Exception("Too many retry");
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

            try
            {
                pinger.onfaill(pinger.didConnect, pinger);
            }
            catch (Exception)
            {

            }            
        }

        pinger.s = null;
        pinger.available = true;
    }
}
