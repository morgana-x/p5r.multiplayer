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

        public int[] Field = new int[2] { -1, -1 };

        public int Model = 0;

        public int Animation = -1;

        public bool RefreshPosition;
        public bool RefreshRotation;
        public bool RefreshField;
        public bool RefreshModel;
        public bool RefreshAnimation;

        public IPAddress IpAddress;

        public IPEndPoint EndPoint;
        public void SendBytes(UdpClient sender, byte[] data )
        {
            sender.Send(data, data.Length,this.EndPoint);
        }
       
    }
}
