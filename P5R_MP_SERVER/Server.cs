using Shared;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static Shared.PacketConnection;

namespace P5R_MP_SERVER
{
    public class Server
    {
        UdpClient udpServer;
        public PacketConnection packetConnection;
        public Dictionary<IPEndPoint, int> IpAddressMap = new Dictionary<IPEndPoint, int>();
        public List<NetworkedPlayer> PlayerList = new List<NetworkedPlayer>();
        DateTime nextHeartbeat = DateTime.Now;

        int MaxNameLength = 16;

        public bool IsInSameField(NetworkedPlayer p1, NetworkedPlayer p2)
        {
            return p1.Field.SequenceEqual(p2.Field) && p1.Field[0] != -1 && p1.Field[2] != -1 ;
        }
        private void NetworkPlayerEntity(NetworkedPlayer player, NetworkedPlayer target)
        {
            target.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_POSITION, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(player.Position[0]),
                                BitConverter.GetBytes(player.Position[1]),
                                BitConverter.GetBytes(player.Position[2])
                            });
            target.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_ROTATION, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(player.Rotation[0]),
                                BitConverter.GetBytes(player.Rotation[1]),
                                BitConverter.GetBytes(player.Rotation[2])
                            });
            target.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_MODEL, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(player.Model),
                            });
        }
        private void NetworkPlayerInfo(NetworkedPlayer player, NetworkedPlayer target)
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(player.Name);
            target.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_NAME, new List<byte[]> {
                                BitConverter.GetBytes(player.Id),
                                BitConverter.GetBytes(nameBytes.Length),
                                nameBytes
                            });
            target.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_FIELD, new List<byte[]> {
                        BitConverter.GetBytes(player.Id),
                        BitConverter.GetBytes(player.Field[0]),
                        BitConverter.GetBytes(player.Field[1]),
                    });
        }

        public void SendReliablePacket(NetworkedPlayer receiver, Packet.P5_PACKET type, List<byte[]> args)
        {
            if (receiver.EndPoint == null)
                return;
            packetConnection.SendReliablePacket(type, args, receiver.EndPoint);
        }
        public void Tick()
        {
            Thread.Sleep(10);
            if (DateTime.Now > nextHeartbeat)
            {
                nextHeartbeat = DateTime.Now.AddMilliseconds(500); //Runtime.CurrentRuntime + 500;
                foreach (NetworkedPlayer pl in PlayerList) // Send heartbeat
                {
                    pl.SendBytes(udpServer, Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { new byte[] {0x0} }));
                }
            }
            foreach (NetworkedPlayer pl in PlayerList)
            {
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
                            continue;
                        p.SendBytes(udpServer, data);
                    }

                }

                if (pl.RefreshField)
                {
                    pl.RefreshField = false;
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                            continue;
                        p.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_FIELD, new List<byte[]> {
                                BitConverter.GetBytes(pl.Id),
                                BitConverter.GetBytes(pl.Field[0]),
                                BitConverter.GetBytes(pl.Field[1]),
                                BitConverter.GetBytes(pl.Field[2]),
                            });
                        if (IsInSameField(p, pl))
                        {
                            NetworkPlayerEntity(p, pl);
                            NetworkPlayerEntity(pl, p);
                        }
                    }
                }
                if (pl.RefreshModel)
                {
                    pl.RefreshModel = false;
                    foreach (NetworkedPlayer p in PlayerList)
                    {
                        if (p.Id == pl.Id)
                            continue;
                        NetworkPlayerEntity(pl, p);
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
                            continue;
                        if (!IsInSameField(p, pl))
                            continue;
                        p.SendBytes(udpServer,packet);
                    }
                }
            }
        }
        private void RemovePlayer(NetworkedPlayer player)
        {
            foreach (NetworkedPlayer p in PlayerList)
            {
                if (p.Id == player.Id)
                    continue;
                p.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_DISCONNECT, new List<byte[]> {
                        BitConverter.GetBytes(player.Id),
                });
            }
            PlayerList.Remove(player);
            IpAddressMap.Remove(player.EndPoint);
        }
        public void KickPlayer(NetworkedPlayer player)
        {
            RemovePlayer(player);
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
                    return;

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
            if (args.RawData.Length < 1)
                return;
            HandlePlayerPacket(args.Endpoint, args.Packet);
        }

        public void HandlePlayerPacket(IPEndPoint endPoint, Packet packet)
        {
            if (packet == null || packet.PacketType == null)
                return;
            if (!IpAddressMap.ContainsKey(endPoint))
                HandlePlayerConnection(endPoint);

            int pId = IpAddressMap[endPoint];

            NetworkedPlayer player = getPlayerFromId(pId);
            if (player == null)
                return;
            if (packet.PacketType == Packet.P5_PACKET.PACKET_NONE || packet.PacketType == Packet.P5_PACKET.PACKET_HEARTBEAT)
                return;

           
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_FIELD)
            {
                player.Field = new int[] { BitConverter.ToInt32(packet.Arguments[1]), BitConverter.ToInt32(packet.Arguments[2]), BitConverter.ToInt32(packet.Arguments[3]) };
                player.RefreshField = true;
                Console.WriteLine($"{player.Id}'s field set to {string.Join("_", player.Field)}.");
            }

            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_POSITION)
            {
                player.Position = new float[] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) };
                player.RefreshPosition = true;
                //Console.WriteLine($"Player position is now {string.Join(",", player.Position)}");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_ROTATION)
            {
                player.Rotation = new float[] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) };
                player.RefreshRotation = true;
                // Console.WriteLine($"Player rotation is now {string.Join(",", player.Position)}");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_MODEL)
            {
                player.Model = BitConverter.ToInt32(packet.Arguments[1]);
                player.RefreshModel = true;
                int[] model = ModelChecker.GetModelFromId(player.Model);
                //Console.WriteLine($"{player.Id}'s model set to {string.Join("_", model)}.");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_ANIMATION)
            {
                //player.Animation = BitConverter.ToInt32(packet.Arguments[1]);
                // player.RefreshAnimation = true;
                //Console.WriteLine($"{player.Id}'s animation set to {player.Animation}.");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_NAME)
            {
                int nameLength = BitConverter.ToInt32(packet.Arguments[1]);
                if (nameLength > MaxNameLength)
                    nameLength = MaxNameLength;
                string newname = Encoding.UTF8.GetString(packet.Arguments[2], 0, nameLength);
                Console.WriteLine($"Set {player.Id}'s name from {player.Name} to {newname}");
                player.Name = newname;
                foreach (NetworkedPlayer pl in PlayerList)
                {
                    if (player.Id == pl.Id)
                        continue;
                    NetworkPlayerInfo(player, pl);
                }
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
            player.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_ASSIGNID, new List<byte[]> { BitConverter.GetBytes(player.Id) });
            PlayerList.Add(player);

            foreach(NetworkedPlayer pl in PlayerList)
            {
                if (player.Id == pl.Id)
                    continue;
                player.SendReliablePacket(packetConnection, Packet.P5_PACKET.PACKET_PLAYER_CONNECT, new List<byte[]> { BitConverter.GetBytes(pl.Id) });
                NetworkPlayerInfo(pl, player);
                NetworkPlayerInfo(player, pl);
            }
        }
        public void HandleClientConnect(object sender, ClientConnectArgs args)
        {
            HandlePlayerConnection(args.Endpoint);
        }
        Commands commands;

        private void ticktask()
        {
            while (true)
                Tick();
        }
        public Server(int port = 11000)
        {
            udpServer = new UdpClient(port);
            packetConnection = new PacketConnection(udpServer, true);
            packetConnection.OnPacketReceived += HandlePacketReceived;
            packetConnection.OnClientDisconnect += HandleClientDisconnect;
            packetConnection.OnClientConnect += HandleClientConnect;
            commands = new Commands(this);
            ticktask();
        }
    }
}
