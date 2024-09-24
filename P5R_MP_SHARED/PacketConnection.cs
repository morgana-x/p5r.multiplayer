using System.Net;
using System.Net.Sockets;

namespace Shared
{
    public class PacketConnection
    {
        public event EventHandler<PacketReceivedArgs>? OnPacketReceived;

        public event EventHandler<ClientDisconnectArgs>? OnClientDisconnect;
        public event EventHandler<ClientConnectArgs>? OnClientConnect;

        public bool ClosedConnectionToServer = false;

        public bool Running = true;
        public class PacketReceivedArgs : EventArgs
        {
            public PacketReceivedArgs(IPEndPoint endpoint, byte[] data)
            {
                Endpoint = endpoint;
                RawData = data;
                try
                {
                    Packet = Packet.ParsePacket(data);
                }
                catch (Exception ex)
                {
                    Packet = null;
                }
            }

            public IPEndPoint Endpoint { get; set; }
            public byte[] RawData { get; set; }

            public Packet Packet { get; set; }
        }
        public class ClientDisconnectArgs : EventArgs
        {
            public ClientDisconnectArgs(IPEndPoint endpoint)
            {
                Endpoint = endpoint;
            }

            public IPEndPoint Endpoint { get; set; }
        }
        public class ClientConnectArgs : EventArgs
        {
            public ClientConnectArgs(IPEndPoint endpoint)
            {
                Endpoint = endpoint;
            }

            public IPEndPoint Endpoint { get; set; }
        }
        public delegate void PacketReceivedHandler(object? sender, PacketReceivedArgs args);

        //List<IPEndPoint> endPoints = new List<IPEndPoint>();
        Dictionary<IPEndPoint, DateTime> endPoints = new Dictionary<IPEndPoint, DateTime>();

        public int timeOutLimitMilliseconds = 5000;
        public void DisconnectEndpoint(IPEndPoint clientRemoteEP)
        {
            if (endPoints.ContainsKey(clientRemoteEP))
            {
                OnClientDisconnect?.Invoke(this, new ClientDisconnectArgs(clientRemoteEP));
                endPoints.Remove(clientRemoteEP);
            }
        }
        private void CheckTimeout()
        {
            List<IPEndPoint> toBeRemoved = new List<IPEndPoint>();
            foreach (var a in endPoints)
            {
               // Console.WriteLine("Timeout: " + DateTime.Now.Subtract(a.Value).TotalMilliseconds);
                if (DateTime.Now.Subtract(a.Value).TotalMilliseconds > timeOutLimitMilliseconds)
                {
                    toBeRemoved.Add(a.Key);
                }
            }
            foreach(var a in toBeRemoved)
            {
                Console.WriteLine("Timeout for client " + a.ToString());
                DisconnectEndpoint(a);
            }
        }
        private void UpdateTimeout(IPEndPoint clientRemoteEP)
        {
            if (!endPoints.ContainsKey(clientRemoteEP))
                endPoints.Add(clientRemoteEP, DateTime.Now);
            endPoints[clientRemoteEP] = DateTime.Now;
        }

        private void HandlePacketTrafficServer(int port = 11000)
        {
            LocalUdpClient.Client.ReceiveTimeout = 90;
            LocalUdpClient.Client.SendTimeout = 50;
            try
            {
                while (Running)
                {
                    checkReliablePacketTimeout();
                    IPEndPoint clientRemoteEP = new IPEndPoint(IPAddress.Any, port);
                    CheckTimeout();
                    try
                    {
                        byte[] data = LocalUdpClient.Receive(ref clientRemoteEP);

                        if (!endPoints.ContainsKey(clientRemoteEP))
                        {
                            if (data[0] != 72)
                            {
                                continue;
                            }
                            UpdateTimeout(clientRemoteEP);
                            OnClientConnect?.Invoke(this, new ClientConnectArgs(clientRemoteEP));
                        }
                        else
                        {
                            UpdateTimeout(clientRemoteEP);
                            PacketReceivedArgs args = new PacketReceivedArgs(clientRemoteEP, data);
                            PreProcessPacket(args.Packet, clientRemoteEP);
                            OnPacketReceived?.Invoke(this, args);
                        }
                    }
                    catch (SocketException e)
                    {
                        if ((e.SocketErrorCode == SocketError.ConnectionAborted || e.SocketErrorCode == SocketError.ConnectionRefused) && endPoints.ContainsKey(clientRemoteEP))
                        {
                            DisconnectEndpoint(clientRemoteEP);
                        }
                    }
                }
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.ToString());
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }


            Console.WriteLine("Somehow loop was broken!!");
        }

        DateTime lastConnectionWithServer = DateTime.Now;

        private void SendReliablePacketAffirmation(Packet packet, IPEndPoint sender = null)
        {
            byte[] confirmPacket = Packet.FormatPacket(Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE, new List<byte[]> { BitConverter.GetBytes(packet.ReliableId) });
            if (!IsServer)
            {
                LocalUdpClient.SendAsync(confirmPacket, confirmPacket.Length);
                return;
            }
            if (sender == null)
                return;
            LocalUdpClient.SendAsync(confirmPacket, confirmPacket.Length, sender);
        }
        private void PreProcessPacketAffirmReliablePackets(Packet packet, IPEndPoint sender = null)
        {
            if (packet.Id == Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE) // sneaky!
                return;
            if (!packet.IsReliable())
                return;
            receivedConfirmedPacketsToIgnore.Add(packet.ReliableId, DateTime.Now.AddMilliseconds(500));
            SendReliablePacketAffirmation(packet, sender);
        }
        private class reliablePacket
        {
            public DateTime time;
            public byte[] packet;
            public IPEndPoint remoteEndPoint;
        }

        Dictionary<short, reliablePacket> reliablePacketsUnconfirmed = new Dictionary<short, reliablePacket>();
        List<short> reliablepacketsConfirmedOrTimedOut = new List<short>();

        Dictionary<short, DateTime> receivedConfirmedPacketsToIgnore = new Dictionary<short, DateTime>();

        short baseId = 1;
        private short getUniqueReliablePacketId()
        {
            if (baseId >= 599)
                baseId = 1;
            for (short i = baseId; i < 600; i++)
            {
                if (!reliablePacketsUnconfirmed.ContainsKey(i))
                {
                    baseId = i;
                    return i;
                }
            }
            return baseId;
        }
        DateTime nextReliablePacketSend = DateTime.Now;

        private bool hasPacketBeenSent(short id)
        {
            return receivedConfirmedPacketsToIgnore.ContainsKey(id);
        }


        private void checkReliablePacketTimeout()
        {
            try
            {
                List<short> ignorePacketsToBeRemoved = new List<short>();
                foreach (var a in receivedConfirmedPacketsToIgnore)
                {
                    if (DateTime.Now > a.Value)
                        ignorePacketsToBeRemoved.Add(a.Key);
                }
                foreach (var a in ignorePacketsToBeRemoved)
                    receivedConfirmedPacketsToIgnore.Remove(a);

                bool send = false;
                if (DateTime.Now > nextReliablePacketSend)
                {
                    send = true;
                    nextReliablePacketSend = DateTime.Now.AddMilliseconds(300);
                }
                foreach (var a in reliablePacketsUnconfirmed)
                {
                    if (DateTime.Now.Subtract(a.Value.time).TotalMilliseconds > 3000)
                    {
                        Console.WriteLine("Reliable packet timed out " + a.Key);
                        if (!reliablepacketsConfirmedOrTimedOut.Contains(a.Key))
                            reliablepacketsConfirmedOrTimedOut.Add(a.Key);
                        continue;
                    }
                    if (send)
                    {
                        if (a.Value.remoteEndPoint != null)
                            LocalUdpClient.SendAsync(a.Value.packet, a.Value.packet.Length, a.Value.remoteEndPoint);
                        else
                            LocalUdpClient.SendAsync(a.Value.packet);
                        Console.WriteLine("Sent reliable packet again " + a.Key);
                    }
                }
                foreach (var a in reliablepacketsConfirmedOrTimedOut)
                {
                    if (reliablePacketsUnconfirmed.ContainsKey(a))
                        reliablePacketsUnconfirmed.Remove(a);
                }
                reliablepacketsConfirmedOrTimedOut.Clear();
            }
            catch(Exception e)
            {
                Console.WriteLine(e.ToString());
            }
        }
        private void PreProcessPacket(Packet packet, IPEndPoint sender=null)
        {
            if (packet == null)
                return;
            if (packet.IsReliable() && hasPacketBeenSent(packet.ReliableId))
            {
                SendReliablePacketAffirmation(packet, sender);
                return;
            }
            PreProcessPacketAffirmReliablePackets(packet, sender);
            if (packet.Id == Packet.P5_PACKET.PACKET_CONFIRM_RECEIVE)
            {
                short packetId = BitConverter.ToInt16(packet.Arguments[0]);
                if (!reliablePacketsUnconfirmed.ContainsKey(packetId))
                    return;
                if (IsServer)
                {
                    //reliablePacket playerWhoSent = null;
                   // reliablePacketsUnconfirmed.TryGetValue(packetId, out playerWhoSent);

                    /*Console.WriteLine("Checking stuff...");
                     *  if (playerWhoSent != null)
                    if (playerWhoSent.remoteEndPoint != sender)
                        return;
                    Console.WriteLine("Checked stuff!");*/
                }
                reliablepacketsConfirmedOrTimedOut.Add(packetId);
            }
        }

        public void SendReliablePacket(Packet.P5_PACKET type, List<byte[]> args, IPEndPoint recipitent=null)
        {
            short packetId = getUniqueReliablePacketId();
            byte[] packetData = Packet.FormatPacket((int)type, args, packetId);
            reliablePacketsUnconfirmed.Add(packetId, new reliablePacket() { packet = packetData, time = DateTime.Now, remoteEndPoint = recipitent});
            if (!IsServer)
                LocalUdpClient.SendAsync(packetData);
            else if (recipitent != null)
                LocalUdpClient.SendAsync(packetData, packetData.Length, recipitent);
        }
        private void HandlePacketTrafficClient()
        {
            LocalUdpClient.Client.ReceiveTimeout = 60;
            LocalUdpClient.Client.SendTimeout = 80;
            try
            {
                while (true)
                {
                    //Console.WriteLine("Waiting for data...");
                    checkReliablePacketTimeout();
                    try
                    {
                        byte[] data = LocalUdpClient.Receive(ref ServerConnection); // listen on port 11000
                        EventHandler<PacketReceivedArgs>? raiseEvent = OnPacketReceived;
                        PacketReceivedArgs args = new PacketReceivedArgs(ServerConnection, data);
                        lastConnectionWithServer = DateTime.Now;
                        PreProcessPacket(args.Packet);
                        if (raiseEvent != null)
                        {
                            // Console.WriteLine("Activating recieved packet event!");
                            raiseEvent(this, args);
                        }
                        else
                        {
                            //   Console.WriteLine("Failed to activated received packet event!");
                        }
                    }
                    catch (SocketException e)
                    {
                        if (e.SocketErrorCode != SocketError.TimedOut)
                            Console.WriteLine(e.SocketErrorCode.ToString());
                        if (e.SocketErrorCode == SocketError.ConnectionRefused)
                        {
                            Console.WriteLine("Host refused connection!");
                            break;
                        }
                        if (DateTime.Now.Subtract(lastConnectionWithServer).TotalMilliseconds > timeOutLimitMilliseconds)
                        {
                            Console.WriteLine("Timed out with server!");
                            break;
                        }
                    }

                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }
            ClosedConnectionToServer = true;
            Console.WriteLine("Connection closed!");

        }

        public void Cleanup()
        {
            Running = false;
            
        }
        Thread NetworkTrafficTask;
        IPEndPoint ServerConnection;
        UdpClient LocalUdpClient;
        bool IsServer = false;
        public PacketConnection(UdpClient localUdpClient, bool server, IPEndPoint serverConnection = null, int serverport = 11000)
        {
            LocalUdpClient = localUdpClient;
            if (server)
            {
                IsServer = true;
                NetworkTrafficTask = new Thread(() => { HandlePacketTrafficServer(serverport); });
                NetworkTrafficTask.IsBackground = true; 
                NetworkTrafficTask.Start();
                return;
            }
            ServerConnection = serverConnection;
            NetworkTrafficTask = new Thread(HandlePacketTrafficClient);
            NetworkTrafficTask.IsBackground = true;
            NetworkTrafficTask.Start();
        }
    }
}
