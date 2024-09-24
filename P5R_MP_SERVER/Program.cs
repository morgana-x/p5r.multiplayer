using P5R_MP_SERVER;
using System.Net;


public partial class Program
{


    private static int ReadPortArgs(string[] args)
    {
        int port = 11000;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--port" && i < args.Length - 1)
            {
                string portnum = args[i + 1];
                try
                {
                    port = int.Parse(portnum);
                }
                catch (Exception ex)
                {
                }
            }
        }
        return port;
    }
    private static string getIpAddress()
    {
        var host = Dns.GetHostEntry(Dns.GetHostName());

        foreach (IPAddress ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                return ip.ToString();
            }
        }
        return string.Empty;
    }
    private static void PrintInfo(int port)
    {
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write($"Started Server at ");
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.Write($"{GetPublicIPv4Address()}");
        Console.ForegroundColor = ConsoleColor.Gray;
        Console.Write(":");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.Write($"{port}\n");
        Console.ForegroundColor = ConsoleColor.Gray;
    }
    private static string GetPublicIPv4Address() => new System.Net.Http.HttpClient().GetStringAsync("http://ifconfig.me").GetAwaiter().GetResult().Replace("\n", "");
    public static void Main(string[] args)
    {
        int port = ReadPortArgs(args);

        Server server = new Server(port);
        PrintInfo(port);
        while (true)
        {
            server.Tick();
        }
    }
}
