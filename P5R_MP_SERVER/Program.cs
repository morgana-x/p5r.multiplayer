using P5R_MP_SERVER;

public partial class Program
{
    public static void Main(string[] args)
    {
        int port = 11000;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (arg == "--port" && i < args.Length-1)
            {
                string portnum = args[i+1];
                try
                {
                    port = int.Parse(portnum);
                }
                catch (Exception ex)
                {
                }
            }
        }
        Server server = new Server(port);
        server.TickTask();
        server.packetConnection.Cleanup();
    }
}
