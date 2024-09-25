using P5R_MP_SERVER;
using Shared;
using System.Security.Cryptography;

public class Commands
{
    private Server server;
    public Commands(Server server)
    {
        this.server = server;

        Task.Run(Tick);
    }

    public Dictionary<string, Action<Server, string[]>> commands = new Dictionary<string, Action<Server, string[]>>()
    {
        ["kick"] = (Server server, string[] args) => {
            if (args.Length == 0)
            {
                Console.WriteLine("Incorrect args! kick [player id]");
                return;
            }
       
            try
            {
                int pId = int.Parse(args[0]);
                NetworkedPlayer pl = server.getPlayerFromId(pId);
                if (pl == null)
                {
                    Console.WriteLine($"Player \"{pId}\" not found!");
                    return;
                }
                server.KickPlayer(pl);
                Console.WriteLine($"Kicked Player \"{pId}\"!");
            }
            catch (Exception ex)
            {
                string name = args[0].ToLower();
                NetworkedPlayer pl = null;
                foreach(var p in server.PlayerList)
                {
                    if (p.Name.ToLower().StartsWith(name) || p.Name.ToLower().Equals(name))
                    {
                        pl = p;
                        break;
                    }
                }
                if (pl == null)
                {
                    Console.WriteLine($"Player \"{name}\" not found!");
                    return;
                }
                server.KickPlayer(pl);
                Console.WriteLine($"Kicked Player \"{name}\"!");
            }
        }
    };


    //string currentInput = "";
    public void Tick()
    {
        while (true)
        {
            string currentInput = Console.ReadLine();
            string[] split = currentInput.Split(" ");
            if (split.Length == 0)
                continue;
            string cmd = split[0];
            string[] args = split.ToList().Slice(1, split.Length - 1).ToArray();
            if (!commands.ContainsKey(cmd))
            {
                Console.WriteLine($"Command \"{cmd}\" doesn't exist!");
                continue;
            }
            commands[cmd].Invoke(server, args);
        }
    }
}
