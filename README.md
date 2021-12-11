# MCServerPinger
A light weight project to read the motd of a minecraft sever

# Example Usage

```csharp
using System.Net;
using MinecraftPinger;

bool ready = true;

DateTime t0 = DateTime.Now;

Pinger p = new Pinger(
    (string motd, Pinger pinger) =>
    {
        Console.WriteLine($"Received from {pinger.ep} ({(int)(DateTime.Now - t0).TotalMilliseconds}ms):\n{motd}");
        ready = true;
    },
    (bool didconnect, Pinger pinger) =>
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
```
