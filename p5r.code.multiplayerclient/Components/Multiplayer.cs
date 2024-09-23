using Reloaded.Mod.Interfaces;
using Shared;
using System.Net.Sockets;
using System.Net;
using static Shared.PacketConnection;
using System.Diagnostics;
using P5R_MP_SERVER;
using p5r.code.multiplayerclient.Configuration;
using System.Text;
using System.Numerics;
using System.Reflection;


namespace p5r.code.multiplayerclient.Components
{

    internal class Multiplayer
    {
        NpcManager _npcManager;
        ILogger _logger;
        Config _config;

        UdpClient Client;
        PacketConnection PacketConnectionHandler;

        int clientPlayerId = -1;

        bool running = false;

        Process process;

        Thread tickThread;

        public Dictionary<int, NetworkedPlayer> PlayerList = new Dictionary<int,NetworkedPlayer>();
        public Multiplayer(NpcManager npcManager, ILogger logger, Config config)
        {
            _npcManager = npcManager;
            _logger = logger;
            _config = config;
            Client = new UdpClient();
            process = Process.GetCurrentProcess();
        }
        public void Cleanup()
        {
            running = false;
            sentEssentialData = false;
            clientPlayerId = -1;
            _npcManager.playerNpcList.Clear();
            PlayerList.Clear();
            packetsQueue.Clear();
            if (tickThread != null && tickThread.ThreadState == System.Threading.ThreadState.Running)
                tickThread.Join();
            tickThread = null;
            if (PacketConnectionHandler != null)
                PacketConnectionHandler.Cleanup();
            PacketConnectionHandler = null;

            reliablePacketsUnconfirmed.Clear();
        }
        public void Disconnect()
        {
            _logger.WriteLine("Disconnecting!");
            Client.Close();
            Cleanup();
        }
        bool sentEssentialData = false;

        private void SendClientInfoToServer()
        {
            if (sentEssentialData)
                return;
            if (!running)
                return;
            if (clientPlayerId == -1)
                return;

            string name = _config.ClientName;
            byte[] nameBytes = Encoding.UTF8.GetBytes(name);
            int nameLength = nameBytes.Length;
            SendReliablePacket(Packet.P5_PACKET.PACKET_PLAYER_NAME, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(nameLength),
                        nameBytes,
                    });
            sentEssentialData = true;
        }
        public void Connect(string ipaddress, int port)
        {
            Cleanup();
            running = true;
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipaddress), port); // endpoint where server is listening
            Client.Connect(ep);
            Client.Send(new byte[] { 0x48, 0x48 }); // Tell server we are not a fraud!
            PacketConnectionHandler = new PacketConnection(Client, false, ep);
            PacketConnectionHandler.OnPacketReceived += HandlePacketReceived;
            Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { BitConverter.GetBytes(78) }));
            Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { BitConverter.GetBytes(78) }));
            // send data
            tickThread = new Thread(TickTask);
            tickThread.IsBackground = true;
            tickThread.Start();
        }

        float[] lastPos = new float[3] { 0, 0, 0 };
        float[] lastRot = new float[3] { 0, 0, 0 };

        int[] lastModel = new int[3] { 1, 1, 0 };

        int lastAnimation = -1;

        List<Packet> packetsQueue = new List<Packet>(); 
        private void Tick()
        {
            SendClientInfoToServer();
            DoPacketQueue();
            if (!_npcManager._p5rLib.FlowCaller.Ready())
            {
                return;
            }
            if (_npcManager.FIELD_CHECK_CHANGE())
            {
                SendReliablePacket(Packet.P5_PACKET.PACKET_PLAYER_FIELD, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(_npcManager.CurrentField[0]),
                        BitConverter.GetBytes(_npcManager.CurrentField[1]),
                    });
            }
            if (_npcManager.CurrentField[0] == -1 && _npcManager.CurrentField[1] == -1)
                return;
            int pcHandle = _npcManager.PC_GET_HANDLE();
            if (pcHandle == -1)
            {
                return;
            }
            int[] newModel = _npcManager.PC_GET_MODEL(pcHandle);
            if (!lastModel.SequenceEqual(newModel))
            {
                lastModel = newModel;
                int modelId = ModelChecker.GetModelId(newModel);

                SendReliablePacket(Packet.P5_PACKET.PACKET_PLAYER_MODEL, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(modelId),
                    });
            }
            float[] newPos = _npcManager.PC_GET_POS(pcHandle);
            float[] newRot = _npcManager.PC_GET_ROT(pcHandle);
            if (!lastPos.SequenceEqual(newPos))
            {
                lastPos = newPos;
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_POSITION, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(newPos[0]),
                        BitConverter.GetBytes(newPos[1]),
                        BitConverter.GetBytes(newPos[2]),
                    }));
            }
            if (!lastRot.SequenceEqual(newRot))
            {
                lastRot = newRot;
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ROTATION, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(newRot[0]),
                        BitConverter.GetBytes(newRot[1]),
                        BitConverter.GetBytes(newRot[2]),
                    }));
            }
            int newAnimation = _npcManager.PC_GET_ANIM(pcHandle);
            if (newAnimation != lastAnimation)
            {
                lastAnimation = newAnimation;
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ANIMATION, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(newAnimation),
                    }));
            }
        }
 
        private void TickTask()
        {
            Thread.Sleep(100);
            while (running)
            {
                Thread.Sleep(50);
                if (PacketConnectionHandler.ClosedConnectionToServer)
                {
                    break;
                }     
                try
                {
                    Tick();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
        }
        void DoPacketQueue()
        {
            List<Packet> packetsToRemove = new List<Packet>();
            for (int i = 0; i < packetsQueue.Count; i++)
            {
                if (i >= packetsQueue.Count)
                    break;
                Packet packet = packetsQueue[i];
                packetsToRemove.Add(packet);
                try
                {
                    HandlePacket(packet);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.ToString());
                }
            }
            foreach (Packet packet in packetsToRemove)
            {
                packetsQueue.Remove(packet);
            }


            //  Check if reliable packets that we sent were received or timedout, and try to send again if so
           /* List<short> reliablepacketsToRemove = new List<short>();
            foreach (var pair in reliablePacketsUnconfirmed)
            {
                if (DateTime.Now.Subtract(pair.Value.time).TotalMilliseconds > 500)
                {
                    reliablepacketsToRemove.Add(pair.Key);
                    continue;
                }
                if (DateTime.Now.Subtract(pair.Value.time).Milliseconds>100)
                {
                    Client.Send(pair.Value.packet);
                }
            }
            foreach(var packet in reliablepacketsToRemove)
            {
                reliablepacketsToRemove.Remove(packet);
            }*/
        }
        private void AddPlayer(int netId)
        {
            if (PlayerList.ContainsKey(netId))
            {
                PlayerList.Remove(netId);
            }
            PlayerList.Add(netId, new NetworkedPlayer() { Id = netId});
        }
        private void RemovePlayer(int netId)
        {
            if (PlayerList.ContainsKey(netId))
            {
                Console.WriteLine($"{PlayerList[netId].Name}({netId}) disconnected!");
                PlayerList.Remove(netId);
            }
            _npcManager.MP_REMOVE_PLAYER(netId);
        }
        private NetworkedPlayer getPlayer(int netId)
        {
            if (!PlayerList.ContainsKey(netId))
            {
                AddPlayer(netId);
            }
            return PlayerList[netId];
        }
        private class reliablePacket
        {
            public DateTime time;
            public byte[] packet;
        }

        Dictionary<short, reliablePacket> reliablePacketsUnconfirmed = new Dictionary<short, reliablePacket>();
        private short getUniqueReliablePacketId()
        {
            short id = 0;
            for (short i = 0; i < 1000; i++)
            {
                if (!reliablePacketsUnconfirmed.ContainsKey(i))
                {
                    id = i;
                    break;
                }
            }
            return id;
        }
        private void SendReliablePacket(Packet.P5_PACKET type, List<byte[]> data)
        {
            short packetId = getUniqueReliablePacketId();
            byte[] packetData = Packet.FormatPacket((int)type, data, getUniqueReliablePacketId());
            //reliablePacketsUnconfirmed.Add(packetId, new reliablePacket() { packet = packetData, time = DateTime.Now});
            Client.Send(packetData);
        }
        private void HandlePacket(Packet packet)
        {
            if (packet.IsReliable())
            {
                Console.WriteLine("Packet is reliable!");
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE, new List<byte[]> { BitConverter.GetBytes(packet.ReliableId) }));
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE)
            {
                short id = BitConverter.ToInt16(packet.Arguments[0]);
                // Todo: Add reliable packet logic here!
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_ASSIGNID)
            {
                clientPlayerId = BitConverter.ToInt32(packet.Arguments[0]);
                _logger.WriteLine("Set local id to " + clientPlayerId);
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_CONNECT)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                //_npcManager.MP_SPAWN_PLAYER(id);
                AddPlayer(id);
                _logger.WriteLine($"Player {id} conneceted!");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_REMOVE)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                _npcManager.MP_REMOVE_PLAYER(id);
               // _logger.WriteLine($"Player {id} hidden!");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_DISCONNECT)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                //_npcManager.MP_REMOVE_PLAYER(id);
                NetworkedPlayer player = getPlayer(id);
                RemovePlayer(id);
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_POSITION)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                _npcManager.MP_SYNC_PLAYER_POS(id, new float[3] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) });
                return;
            }

            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_ROTATION)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                _npcManager.MP_SYNC_PLAYER_ROT(id, new float[3] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) });
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_MODEL)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int modelId = BitConverter.ToInt32(packet.Arguments[1]);
                int[] model = ModelChecker.GetModelFromId(modelId);
                try
                {
                    _npcManager.MP_SYNC_PLAYER_MODEL(id, model[0], model[1], model[2]);
                }
                catch(Exception e) 
                {
                    Console.WriteLine(e.ToString());
                }
                NetworkedPlayer player = getPlayer(id);
                Console.WriteLine($"{player.Name}({id})'s model set to {string.Join("_", model)}.");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_FIELD)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int field_major = BitConverter.ToInt32(packet.Arguments[1]);
                int field_minor = BitConverter.ToInt32(packet.Arguments[2]);
                _npcManager.MP_PLAYER_SET_FIELD(id, new int[] { field_major, field_minor });
                NetworkedPlayer player = getPlayer(id);
                _logger.WriteLine($"{player.Name}({id})'s field set to {string.Join("_", new int[] { field_major, field_minor })}.");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_ANIMATION)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int animation = BitConverter.ToInt32(packet.Arguments[1]);
                _npcManager.MP_SYNC_PLAYER_ANIMATION(id, animation);
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_NAME)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int nameLength = BitConverter.ToInt32(packet.Arguments[1]);
                if (nameLength > 16)
                    nameLength = 16;
                string newname = Encoding.UTF8.GetString(packet.Arguments[2], 0, nameLength);
                NetworkedPlayer player = getPlayer(id);
                _logger.WriteLine($"Set {player.Id}'s name from {player.Name} to {newname}");
                player.Name = newname;
                return;
            }
        }
        private void HandlePacketReceived(object sender, PacketReceivedArgs args)
        {
            Packet packet = Packet.ParsePacket(args.Data);
            if (packet == null)
            {
                Console.WriteLine("Error parsing packet!");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_HEARTBEAT)
            {
                // Heartbeat
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { BitConverter.GetBytes(78) }));
                return;
            }
            packetsQueue.Add(packet);
        }

    }
}
