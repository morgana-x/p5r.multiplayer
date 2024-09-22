using System.Net.Sockets;
using System.Net;
using static System.Runtime.InteropServices.JavaScript.JSType;

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
                Data = data;
            }

            public IPEndPoint Endpoint { get; set; }
            public byte[] Data { get; set; }
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
                if (DateTime.Now.Subtract(a.Value).Milliseconds > LocalUdpClient.Client.ReceiveTimeout)
                {
                    toBeRemoved.Add(a.Key);
                }
            }
            foreach(var a in toBeRemoved)
            {
                DisconnectEndpoint(a);
            }
        }


        private void HandlePacketTrafficServer(int port = 11000)
        {
            LocalUdpClient.Client.ReceiveTimeout = 5000;
            LocalUdpClient.Client.SendTimeout = 5000;
            try
            {
                while (Running)
                {
                    CheckTimeout();
                    IPEndPoint clientRemoteEP = new IPEndPoint(IPAddress.Any, port);
                    try
                    {
                        byte[] data = LocalUdpClient.Receive(ref clientRemoteEP);
                        if (!endPoints.ContainsKey(clientRemoteEP))
                        {
                            if (data[0] != 72)
                            {
                                continue;
                            }
                            //endPoints.Add(clientRemoteEP);
                            endPoints.Add(clientRemoteEP, DateTime.Now);
                            OnClientConnect?.Invoke(this, new ClientConnectArgs(clientRemoteEP));
                        }
                        else
                        {
                            endPoints[clientRemoteEP] = DateTime.Now;
                            OnPacketReceived?.Invoke(this, new PacketReceivedArgs(clientRemoteEP, data));
                        }
                    }
                    catch (SocketException e)
                    {
                        if (endPoints.ContainsKey(clientRemoteEP))
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
        private void HandlePacketTrafficClient()
        {


            LocalUdpClient.Client.ReceiveTimeout = 1000;
            // LocalUdpClient.Client.SendTimeout = 5000;
            try
            {
                while (true)
                {
                    //Console.WriteLine("Waiting for data...");
                    byte[] data = LocalUdpClient.Receive(ref ServerConnection); // listen on port 11000
                    EventHandler<PacketReceivedArgs>? raiseEvent = OnPacketReceived;
                    PacketReceivedArgs args = new PacketReceivedArgs(ServerConnection, data);


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
            }
            catch (SocketException e)
            {
                Console.WriteLine(e.SocketErrorCode.ToString());
            }
            catch (Exception e)
            {

            }
            finally
            {
                ClosedConnectionToServer = true;
                Console.WriteLine("Connection closed!");
            }

        }

        public void Cleanup()
        {
            Running = false;
            
        }
        Thread NetworkTrafficTask;
        IPEndPoint ServerConnection;
        UdpClient LocalUdpClient;
        public PacketConnection(UdpClient localUdpClient, bool server, IPEndPoint serverConnection = null, int serverport = 11000)
        {
            LocalUdpClient = localUdpClient;
            if (server)
            {
                LocalUdpClient.Client.SendTimeout = 2000;
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
