using Shared;
using System.Net;
using System.Net.Sockets;
using static Shared.PacketConnection;

namespace P5R_MP_SERVER
{
    internal class Server
    {
        UdpClient udpServer;
        public PacketConnection packetConnection;
        public Dictionary<IPEndPoint, int> IpAddressMap = new Dictionary<IPEndPoint, int>();
        public List<NetworkedPlayer> PlayerList = new List<NetworkedPlayer>();
        long nextHeartbeat = 0;

        public bool IsInSameField(NetworkedPlayer p1, NetworkedPlayer p2)
        {
            return p1.Field.SequenceEqual(p2.Field) && p1.Field[0] != -1;
        }
        private void NetworkPlayer(NetworkedPlayer player, NetworkedPlayer target)
        {
            byte[] posData = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_POSITION, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(player.Position[0]),
                                BitConverter.GetBytes(player.Position[1]),
                                BitConverter.GetBytes(player.Position[2])
                            });
            byte[] rotData = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ROTATION, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(player.Rotation[0]),
                                BitConverter.GetBytes(player.Rotation[1]),
                                BitConverter.GetBytes(player.Rotation[2])
                            });
            byte[] modelData = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_MODEL, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(player.Model),
                            });
            target.SendBytes(udpServer, modelData);
            target.SendBytes(udpServer, posData);
            target.SendBytes(udpServer, rotData);
        }
        public void Tick()
        {
            Thread.Sleep(10);
            if (Runtime.CurrentRuntime > nextHeartbeat)
            {
                nextHeartbeat = Runtime.CurrentRuntime + 500;
                foreach (NetworkedPlayer pl in PlayerList) // Send heartbeat
                {
                    pl.SendBytes(udpServer, Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { new byte[] {0x0} }));
                }
            }
            foreach (NetworkedPlayer pl in PlayerList)
            {
                byte[] removePlayerData = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_REMOVE, new List<byte[]> {
                        BitConverter.GetBytes(pl.Id),
                    });
                if (pl.RefreshPosition)
                {
                    pl.RefreshPosition = false;
                    byte[] data = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_POSITION, new List<byte[]> {
                        BitConverter.GetBytes(pl.Id), 
                        BitConverter.GetBytes(pl.Position[0]), 
                        BitConverter.GetBytes(pl.Position[1]), 
                        BitConverter.GetBytes(pl.Position[2]) 
                    });
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                            continue;
                        if (!IsInSameField(p, pl))
                            continue;
                        p.SendBytes(udpServer, data);
                    }

                }
                if (pl.RefreshRotation)
                {
                    pl.RefreshRotation = false;
                    byte[] data = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ROTATION, new List<byte[]> {
                        BitConverter.GetBytes(pl.Id),
                        BitConverter.GetBytes(pl.Rotation[0]),
                        BitConverter.GetBytes(pl.Rotation[1]),
                        BitConverter.GetBytes(pl.Rotation[2])
                    });
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                            continue;
                        if (!IsInSameField(p, pl))
                        {
                            continue;
                        }
                        p.SendBytes(udpServer, data);
                    }

                }

                if (pl.RefreshField)
                {
                    pl.RefreshField = false;
                    byte[] data = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_FIELD, new List<byte[]> {
                        BitConverter.GetBytes(pl.Id),
                        BitConverter.GetBytes(pl.Field[0]),
                        BitConverter.GetBytes(pl.Field[1]),
                    });
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                        {
                            continue;
                        }
                        if (IsInSameField(p, pl))
                        {
                            NetworkPlayer(p, pl);
                            NetworkPlayer(pl, p);
                        }
                        else
                        {
                            p.SendBytes(udpServer,removePlayerData);
                        }
                        p.SendBytes(udpServer, data);
                    }
                }
                if (pl.RefreshModel)
                {
                    pl.RefreshModel = false;
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                        {
                            continue;
                        }
                        if (!IsInSameField(p, pl))
                        {
                            p.SendBytes(udpServer, removePlayerData);
                            continue;
                        }
                        NetworkPlayer(pl, p);
                    }
                }
                if (pl.RefreshAnimation)
                {
                    pl.RefreshAnimation = false;
                    byte[] packet = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ANIMATION, new List<byte[]> 
                    {
                        BitConverter.GetBytes(pl.Id),
                        BitConverter.GetBytes(pl.Animation)
                    });
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                        {
                            continue;
                        }
                        if (!IsInSameField(p, pl))
                        {
                            continue;
                        }
                        p.SendBytes(udpServer,packet);
                    }
                }
            }
        }
        private void RemovePlayer(NetworkedPlayer player)
        {
            byte[] data = Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_DISCONNECT, new List<byte[]> {
                        BitConverter.GetBytes(player.Id),
                    });
            foreach (NetworkedPlayer p in PlayerList)
            {
                if (p.Id == player.Id)
                {
                    continue;
                }
                p.SendBytes(udpServer, data);
            }
            PlayerList.Remove(player);
            IpAddressMap.Remove(player.EndPoint);
        }
        public NetworkedPlayer getPlayerFromId(int id)
        {
            foreach(var p in PlayerList)
            {
                if (p.Id == id)
                    return p;
            }

            return null;
        }
        public void HandleClientDisconnect(object sender, ClientDisconnectArgs args)
        {
            try
            {
                if (!IpAddressMap.ContainsKey(args.Endpoint))
                {
                    return;
                }
                NetworkedPlayer pl = getPlayerFromId(IpAddressMap[args.Endpoint]);
                if (pl == null)
                    return;
                RemovePlayer(pl);
                Console.WriteLine(pl.Name + " Disconnected!");        
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        public void HandlePacketReceived(object sender, PacketReceivedArgs args)
        {
            if (args.Data.Length < 1)
            {
                return;
            }
            HandlePlayerPacket(args.Endpoint, args.Data);
        }

        public void HandlePlayerPacket(IPEndPoint endPoint, byte[] data)
        {
            if (data.Length < 1)
            {
                return;
            }
            if (!IpAddressMap.ContainsKey(endPoint))
            {
                Console.WriteLine("Unverified player cannot handle packet!");
                HandlePlayerConnection(endPoint);
            }
            int pId = IpAddressMap[endPoint];

            NetworkedPlayer player = getPlayerFromId(pId);
            if (player == null)
                return;

            Packet packet = Packet.ParsePacket(data);
            if (packet == null || packet.Id == null)
            {
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_NONE || packet.Id == Packet.P5_PACKET.PACKET_HEARTBEAT)
            {
                return;
            }
            if (packet.IsReliable())
            {
                Console.WriteLine("Packet is reliable!");
                player.SendBytes(udpServer, Packet.FormatPacket(Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE, new List<byte[]> { BitConverter.GetBytes(packet.ReliableId) }));
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_FIELD)
            {
                player.Field = new int[] { BitConverter.ToInt32(packet.Arguments[1]), BitConverter.ToInt32(packet.Arguments[2]) };
                player.RefreshField = true;
                Console.WriteLine($"{player.Id}'s field set to {string.Join("_", player.Field)}.");
            }

            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_POSITION)
            {
                player.Position = new float[] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) };
                player.RefreshPosition = true;
                //Console.WriteLine($"Player position is now {string.Join(",", player.Position)}");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_ROTATION)
            {
                player.Rotation = new float[] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) };
                player.RefreshRotation = true;
                // Console.WriteLine($"Player rotation is now {string.Join(",", player.Position)}");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_MODEL)
            {
                player.Model = BitConverter.ToInt32(packet.Arguments[1]);
                player.RefreshModel = true;
                int[] model = ModelChecker.GetModelFromId(player.Model);
                //Console.WriteLine($"{player.Id}'s model set to {string.Join("_", model)}.");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_ANIMATION)
            {
                player.Animation = BitConverter.ToInt32(packet.Arguments[1]);
                player.RefreshAnimation = true;
                //Console.WriteLine($"{player.Id}'s animation set to {player.Animation}.");
                return;
            }
        }

        private int getFreePlayerId()
        {
            int id = 0;
            bool foundId = false;
            while (true)
            {
                foundId = true;
                foreach (NetworkedPlayer p in PlayerList)
                {
                    if (p.Id == id)
                    {
                        foundId = false;
                        id++;
                        break;
                    }
                }
                if (foundId)
                    break;
            }
            return id;
        }
        public void HandlePlayerConnection(IPEndPoint endPoint)
        {
            if (IpAddressMap.ContainsKey(endPoint))
            {
                return;
            }
            Console.WriteLine("New player " + endPoint.Address + " joined!");

            int newId = getFreePlayerId();

            NetworkedPlayer player = new NetworkedPlayer() { Name = "testname", IpAddress = endPoint.Address, Id = newId, EndPoint = endPoint };
            IpAddressMap.Add(endPoint, player.Id);
            player.SendBytes(udpServer, Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ASSIGNID, new List<byte[]> { BitConverter.GetBytes(player.Id) }));
            PlayerList.Add(player);

            foreach(NetworkedPlayer pl in PlayerList)
            {
                if (player.Id == pl.Id)
                    continue;
                player.SendBytes(udpServer, Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_CONNECT, new List<byte[]> { BitConverter.GetBytes(pl.Id) }));
                pl.RefreshPosition = true;
                pl.RefreshRotation = true;
            }
        }
        public void HandleClientConnect(object sender, ClientConnectArgs args)
        {
            HandlePlayerConnection(args.Endpoint);
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
        public Server(int port = 11000)
        {
            udpServer = new UdpClient(port);
            packetConnection = new PacketConnection(udpServer, true);
            packetConnection.OnPacketReceived += HandlePacketReceived;
            packetConnection.OnClientDisconnect += HandleClientDisconnect;
            packetConnection.OnClientConnect += HandleClientConnect;
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write($"Started Server at ");
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write($"{getIpAddress()}");
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.Write(":");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{port}\n");
            Console.ForegroundColor = ConsoleColor.Gray;
        }
    }
}
