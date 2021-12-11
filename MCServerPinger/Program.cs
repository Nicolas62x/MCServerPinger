
using System.Net;

bool ready = true;

DateTime t0 = DateTime.Now;

MinecraftPinger p = new MinecraftPinger(
    (string motd, MinecraftPinger pinger) =>
    {
        Console.WriteLine($"Received from {pinger.ep} ({(int)(DateTime.Now - t0).TotalMilliseconds}ms):\n{motd}");
        ready = true;
    },
    (bool didconnect, MinecraftPinger pinger) =>
    {
        Console.WriteLine($"Failled ping of {pinger.ep}");
        ready = true;
    });
IPEndPoint ep = new IPEndPoint(Dns.GetHostEntry("mcsharp.fr").AddressList[0], 25565);

while (true)
{   

    if (ready)
    {
        t0 = DateTime.Now;
        ready = false;
        p.StartChecking(ep);
    }
    
    Thread.Sleep(1000);
}