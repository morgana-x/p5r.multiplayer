using Reloaded.Mod.Interfaces;
using Shared;
using System.Net.Sockets;
using System.Net;
using static Shared.PacketConnection;
using System.Diagnostics;
using P5R_MP_SERVER;
using p5r.code.multiplayerclient.Utility;


namespace p5r.code.multiplayerclient.Components
{

    internal class Multiplayer
    {
        NpcManager _npcManager;
        ILogger _logger;

        UdpClient Client;
        PacketConnection PacketConnectionHandler;

        int clientPlayerId = -1;

        bool running = false;

        Process process;

        Thread tickThread;
        public Multiplayer(NpcManager npcManager, ILogger logger)
        {
            _npcManager = npcManager;
            _logger = logger;
            Client = new UdpClient();
            process = Process.GetCurrentProcess();
        }
        public void Cleanup()
        {
            running = false;
            _npcManager.playerNpcList.Clear();
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
            if (Input.IsKeydown(0x73))
            {
                Disconnect();
                return;
            }
            DoPacketQueue();
            if (!_npcManager._p5rLib.FlowCaller.Ready())
            {
                return;
            }

            if (_npcManager.FIELD_CHECK_CHANGE())
            {
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_FIELD, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(_npcManager.CurrentField[0]),
                        BitConverter.GetBytes(_npcManager.CurrentField[1]),
                    }));
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
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_PLAYER_MODEL, new List<byte[]>()
                    {
                        BitConverter.GetBytes(clientPlayerId),
                        BitConverter.GetBytes(modelId),
                    }));
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
            List<Packet> packetsToRemove = new List<Packet> ();
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
        private void HandlePacket(Packet packet)
        {
            if (packet.Id == Packet.P5_PACKET.PACKET_HEARTBEAT)
            {
                // Heartbeat
                Client.Send(Packet.FormatPacket(Packet.P5_PACKET.PACKET_HEARTBEAT, new List<byte[]> { BitConverter.GetBytes(78) }));
                return;
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
                _logger.WriteLine($"Player {id} conneceted!");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_REMOVE)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                _npcManager.MP_REMOVE_PLAYER(id);
                _logger.WriteLine($"Player {id} disconneceted!");
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
                Console.WriteLine($"{id}'s model set to {string.Join("_", model)}.");
                try
                {
                    _npcManager.MP_SYNC_PLAYER_MODEL(id, model[0], model[1], model[2]);
                }
                catch(Exception e) 
                {
                    Console.WriteLine(e.ToString());
                }
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_FIELD)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int field_major = BitConverter.ToInt32(packet.Arguments[1]);
                int field_minor = BitConverter.ToInt32(packet.Arguments[2]);
                _npcManager.MP_PLAYER_SET_FIELD(id, new int[] { field_major, field_minor });
                Console.WriteLine($"{id}'s field set to {string.Join("_", new int[] { field_major, field_minor })}.");
                return;
            }
            if (packet.Id == Packet.P5_PACKET.PACKET_PLAYER_ANIMATION)
            {
                int id = BitConverter.ToInt32(packet.Arguments[0]);
                int animation = BitConverter.ToInt32(packet.Arguments[1]);
                _npcManager.MP_SYNC_PLAYER_ANIMATION(id, animation);
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
            packetsQueue.Add(packet);
        }

    }
}
