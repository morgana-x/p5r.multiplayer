using Reloaded.Mod.Interfaces;
using Shared;
using System.Net.Sockets;
using System.Net;
using static Shared.PacketConnection;
using System.Diagnostics;
using P5R_MP_SERVER;
using p5r.code.multiplayerclient.Configuration;
using System.Text;
using p5rpc.lib.interfaces;
using Reloaded.Hooks.Definitions;
using p5r.code.multiplayerclient.Utility;
using System.Collections.Generic;


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
        public Multiplayer(IP5RLib lib, ILogger logger, IReloadedHooks hooks, Config config)
        {
            _npcManager = new NpcManager(lib, logger, hooks, this);
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
            IPEndPoint ep = new IPEndPoint(IPAddress.Parse(ipaddress), port);

            Client.Connect(ep);
            PacketConnectionHandler = new PacketConnection(Client, false, ep);
            PacketConnectionHandler.OnPacketReceived += HandlePacketReceived;

            SendReliablePacket(Packet.P5_PACKET.PACKET_PLAYER_AUTHENTICATE, new List<byte[]> { new byte[]{ 0x48 } });

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
                return;
            if (_npcManager.FIELD_CHECK_CHANGE())
            {
                SendReliablePacket(Packet.P5_PACKET.PACKET_PLAYER_FIELD, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(_npcManager.CurrentField[0]),
                        BitConverter.GetBytes(_npcManager.CurrentField[1]),
                        BitConverter.GetBytes(_npcManager.CurrentField[2]),
                    });
                return;
            }
            if (_npcManager.CurrentField[0] == -1 || _npcManager.CurrentField[1] == -1 || _npcManager.CurrentField[2] == -1)
                return;
            UpdatePlayerPositions();
            int pcHandle = _npcManager.lastPcHandle; //PC_GET_HANDLE(); // PC_GET_HANDLE() caches result, and is called in FIELD_CHECK_CHANGE so calling this again is unessecary
            if (pcHandle == -1)
                return;
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
                return;
            }
            /*int newAnimation = _npcManager.PC_GET_ANIM(pcHandle);
            if (newAnimation != lastAnimation)
            {
                lastAnimation = newAnimation;
                Console.WriteLine(newAnimation);
                Client.SendAsync(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ANIMATION, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(newAnimation),
                    }));
            }*/
            float[] newPos = _npcManager.PC_GET_POS(pcHandle);
            if (!lastPos.SequenceEqual(newPos))
            {
                lastPos = newPos;
                Client.SendAsync(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_POSITION, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(newPos[0]),
                        BitConverter.GetBytes(newPos[1]),
                        BitConverter.GetBytes(newPos[2]),
                    }));
            }
            float[] newRot = _npcManager.PC_GET_ROT(pcHandle);
            if (!lastRot.SequenceEqual(newRot))
            {
                lastRot = newRot;
                Client.SendAsync(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_ROTATION, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(newRot[0]),
                        BitConverter.GetBytes(newRot[1]),
                        BitConverter.GetBytes(newRot[2]),
                    }));
            }

        }
 
        private void TickTask()
        {
            while (running)
            {
                Thread.Sleep(10);
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
        public NetworkedPlayer getPlayer(int netId)
        {
            if (!PlayerList.ContainsKey(netId))
            {
                AddPlayer(netId);
            }
            return PlayerList[netId];
        }

        private void SendReliablePacket(Packet.P5_PACKET type, List<byte[]> args)
        {
            PacketConnectionHandler.SendReliablePacket(type, args);
        }
        private void DoLerp(NetworkedPlayer player)
        {
            if (player.DestPosition.SequenceEqual(player.Position))
                return;
            if (player.StartPosition.SequenceEqual(player.DestPosition))
                return;
            if (DateTime.Now > player.LerpFinish)
            {
                player.Position = player.DestPosition;
                player.Animation = 0;
                player.RefreshAnimation = true;
                player.RefreshPosition = true;
                return;
            }
            player.Position = VecArray.Lerp(player.StartPosition, player.DestPosition, DateTime.Now.Millisecond / player.LerpFinish.Millisecond);
            if (VecArray.Dist(player.Position, player.DestPosition) < 20f)
            {
                player.Animation = 0;
                player.RefreshAnimation = true;
                player.Position = player.DestPosition;
                player.RefreshPosition = true;
                return;
            }
            if (VecArray.Dist(player.Position, player.DestPosition) < 800f)
                player.Animation = 2;
            if (VecArray.Dist(player.Position, player.DestPosition) < 500f)
                player.Animation = 2;
            if (VecArray.Dist(player.Position, player.DestPosition) < 250f)
                player.Animation = 1;
            if (VecArray.Dist(player.Position, player.DestPosition) < 100f)
                player.Animation = 1;
            player.RefreshAnimation = true;

        }
        private void UpdatePlayerPositions()
        {
            foreach (var player in PlayerList.Values)
            {
                bool isInSameField = _npcManager.CurrentField.SequenceEqual(player.Field) && _npcManager.CurrentField[0] != -1 && _npcManager.CurrentField[2] != -1;

                if (player.RefreshField && !isInSameField)
                {
                    player.RefreshField = false;
                    _npcManager.MP_REMOVE_PLAYER(player.Id);
                    continue;
                }
                player.RefreshField = false;
                if (!isInSameField)
                {
                    player.RefreshModel = false;
                    player.RefreshPosition = false;
                    player.RefreshRotation = false;
                    player.RefreshAnimation = false;
                    continue;
                }
                if (player.RefreshModel)
                {
                    player.RefreshModel = false;
                    int[] model = ModelChecker.GetModelFromId(player.Model);
                    _npcManager.MP_SYNC_PLAYER_MODEL(player.Id, model[0], model[1], model[2]);
                    continue;
                }
                if (player.RefreshPosition)
                {
                    player.RefreshPosition = false;
                    _npcManager.MP_SYNC_PLAYER_POS(player.Id, player.Position);
                    if (!player.DestPosition.SequenceEqual(player.Position))
                        _npcManager.MP_SYNC_PLAYER_POS_DEST(player.Id, player.DestPosition);
                }
                DoLerp(player);
                if (player.RefreshAnimation)
                {
                    player.RefreshAnimation = false;
                    if (player.LastAnimation == player.Animation)
                    {
                        return;
                    }
                    player.LastAnimation = player.Animation;
                    _npcManager.MP_SYNC_PLAYER_ANIMATION(player.Id, player.Animation);
                }
  
                if (player.RefreshRotation)
                {
                    player.RefreshRotation = false;
                    _npcManager.MP_SYNC_PLAYER_ROT(player.Id, player.Rotation);
                }
            }
        }
        private void HandlePacket(Packet packet)
        {
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_ASSIGNID)
            {
                clientPlayerId = BitConverter.ToInt32(packet.Arguments[0]);
                _logger.WriteLine("Set local id to " + clientPlayerId);
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_POSITION)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                NetworkedPlayer player = getPlayer(id);
                float[] newpos = new float[3] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) };
                player.Position = player.DestPosition;
                player.DestPosition = newpos;
                float dist = VecArray.Dist(player.Position, player.DestPosition);
                if (dist > 800f)
                    player.Position = VecArray.Middle(player.Position, player.DestPosition);
                if (dist > 1200f)
                    player.Position = player.DestPosition;
                player.LerpFinish = DateTime.Now.AddMilliseconds((dist + 1) * 75);
                player.StartPosition = player.Position;
                player.RefreshPosition = true;
                return;
            }

            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_ROTATION)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                NetworkedPlayer player = getPlayer(id);
                float[] newrot = new float[3] { BitConverter.ToSingle(packet.Arguments[1]), BitConverter.ToSingle(packet.Arguments[2]), BitConverter.ToSingle(packet.Arguments[3]) };
                player.Rotation = newrot;
                player.RefreshRotation = true;
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_CONNECT)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                AddPlayer(id);
                _logger.WriteLine($"Player {id} conneceted!");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_DISCONNECT)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                RemovePlayer(id);
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_MODEL)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int modelId = BitConverter.ToInt32(packet.Arguments[1]);
                int[] model = ModelChecker.GetModelFromId(modelId);
                NetworkedPlayer player = getPlayer(id);
                player.Model = modelId;
                player.RefreshModel = true;
                Console.WriteLine($"{player.Name}({id})'s model set to {string.Join("_", model)}.");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_FIELD)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int field_major = BitConverter.ToInt32(packet.Arguments[1]);
                int field_minor = BitConverter.ToInt32(packet.Arguments[2]);
                int field_posindex = BitConverter.ToInt32(packet.Arguments[3]);
                NetworkedPlayer player = getPlayer(id);
                int[] newfield = new int[] { field_major, field_minor, field_posindex };
                if (player.Field.SequenceEqual(newfield))
                    return;
                player.Field = newfield;
                player.RefreshField = true;
                _logger.WriteLine($"{player.Name}({id})'s field set to {string.Join("_", player.Field)}.");
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_ANIMATION)
            {
                /*int id = BitConverter.ToInt32(packet.Arguments[0]);
                NetworkedPlayer player = getPlayer(id);
                int animation = BitConverter.ToInt32(packet.Arguments[1]);
                player.Animation = animation;
                player.RefreshAnimation = true;*/
                return;
            }
            if (packet.PacketType == Packet.P5_PACKET.PACKET_PLAYER_NAME)
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
            Packet packet = args.Packet;
            if (packet == null)
                return;
            if (packet.PacketType == Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE)
                return;
            if (packet.PacketType == Packet.P5_PACKET.PACKET_HEARTBEAT)
            {
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { BitConverter.GetBytes(78) }));
                return;
            }
            try
            {
                HandlePacket(packet);
            }
            catch (Exception ex)
            {
                packetsQueue.Add(packet);
            }
        }

    }
}
