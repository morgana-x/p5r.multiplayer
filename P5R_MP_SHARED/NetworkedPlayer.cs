using System.Net;
using System.Net.Sockets;

namespace Shared
{
    public class NetworkedPlayer
    {
        public int Id = -1;
        public string Name = "Random Name";

        public float[] Position = new float[3] { 0, 0, 0 };
        public float[] Rotation = new float[3] { 0, 0, 0 };

        public float[] DestPosition = new float[3] {0, 0, 0 };
        public float[] DestRotation = new float[3] {0, 0, 0 };

        public DateTime LerpFinish;
        public float[] StartPosition = new float[3] { 0, 0, 0 };

        public int[] Field = new int[3] { -1, -1, -1 };

        public int Model = 0;

        public int Animation = -1;
        public int LastAnimation = -1;

        public bool RefreshPosition;
        public bool RefreshRotation;
        public bool RefreshField;
        public bool RefreshModel;
        public bool RefreshAnimation;

        public IPAddress IpAddress;

        public IPEndPoint EndPoint;
        public void SendBytes(UdpClient sender, byte[] data, bool syncronous = false)
        {
            if (syncronous)
            {
                sender.Send(data, data.Length, this.EndPoint);
                return;
            }
            sender.SendAsync(data, data.Length,this.EndPoint);
        }
        public void SendPacket(UdpClient sender, Packet.P5_PACKET type, List<byte[]> args)
        {
            byte[] packetData = Packet.FormatPacket(type, args);
            sender.SendAsync(packetData, packetData.Length, this.EndPoint);
        }
        public void SendReliablePacket(PacketConnection connection, Packet.P5_PACKET type, List<byte[]> args)
        {
            connection.SendReliablePacket(type, args, this.EndPoint);
        }
        /*public void SendReliableBytes( PacketConnection connection, byte[] packetData)
        {
            if (this.EndPoint == null)
                return;
            connection.SendReliablePacketBytes(packetData, this.EndPoint);
        }*/

    }
}
