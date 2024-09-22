using P5R_MP_SERVER;

public partial class Program
{
    public static void Main(string[] args)
    {
        Server server = new Server();
        server.TickTask();
        server.packetConnection.Cleanup();
    }
}
