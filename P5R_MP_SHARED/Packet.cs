namespace Shared
{
    public class Packet
    {
        public enum P5_PACKET
        {
            PACKET_NONE,
            PACKET_HEARTBEAT,

            PACKET_PLAYER_POSITION,
            PACKET_PLAYER_ROTATION,
            PACKET_PLAYER_FIELD,
            PACKET_PLAYER_ANIMATION,
            PACKET_PLAYER_MODEL,

            PACKET_PLAYER_ASSIGNID,
            PACKET_PLAYER_CONNECT,
            PACKET_PLAYER_REMOVE,

            PACKET_PLAYER_MESSAGE,
            PACKET_PLAYER_NAME,

            PACKET_REQUEST_PLAYER_DATA
        }
        // num of bytes
        public static Dictionary<P5_PACKET, int[]> P5_PACKET_DATA = new Dictionary<P5_PACKET, int[]>()
        {
            [P5_PACKET.PACKET_NONE] = new int[] { 0 },

            [P5_PACKET.PACKET_HEARTBEAT] = new int[] { 0 },

            [P5_PACKET.PACKET_PLAYER_CONNECT] = new int[] { 4 },

            [P5_PACKET.PACKET_PLAYER_REMOVE] = new int[] { 4 },

            [P5_PACKET.PACKET_PLAYER_POSITION] = new int[] { 4, 4, 4, 4 },

            [P5_PACKET.PACKET_PLAYER_ROTATION] = new int[] { 4, 4, 4, 4 },

            [P5_PACKET.PACKET_PLAYER_MESSAGE] = new int[] { 4, 4, -1 },

            [P5_PACKET.PACKET_PLAYER_NAME] = new int[] { 4, 4, -1 },

            [P5_PACKET.PACKET_PLAYER_ASSIGNID] = new int[] { 4 },

            [P5_PACKET.PACKET_PLAYER_FIELD] = new int[] { 4, 4, 4 },

            [P5_PACKET.PACKET_PLAYER_ANIMATION] = new int[] { 4, 4, 4 },

            [P5_PACKET.PACKET_PLAYER_MODEL] = new int[] { 4, 4}
        };

        public byte[][] Arguments;
        public P5_PACKET Id;

        public Packet(P5_PACKET id, byte[][] arguments)
        {
            Id = id;
            Arguments = arguments;
        }

        public static Packet ParsePacket(byte[] raw)
        {

            if (raw.Length < 8)
            {
                return null;
            }
            int packetId = BitConverter.ToInt32(raw, 0);
            int dataLength = BitConverter.ToInt32(raw, 4);

            if (!P5_PACKET_DATA.ContainsKey((P5_PACKET)packetId))
            {
                return new Packet(P5_PACKET.PACKET_NONE, new byte[][] { new byte[1] {0x0} });
            }

            byte[] rawData = new byte[dataLength];
            ByteUtil.InsertBytes(ref rawData, 0, 8, raw);


            MemoryStream reader = new MemoryStream(rawData);
            reader.Position = 0;

            byte[] lastPacketValue = new byte[4] { 0, 0, 0, 0 };

            List<byte[]> Arguments = new List<byte[]>(){ };
            foreach (int l in P5_PACKET_DATA[(P5_PACKET)packetId])
            {
                int length = l;
                if (l == -1)
                {
                    length = BitConverter.ToInt32(lastPacketValue, 0);
                }
                byte[] buffer = new byte[length];
                reader.Read(buffer);
                lastPacketValue = buffer;
                Arguments.Add(buffer);
            }
            return new Packet((P5_PACKET)packetId, Arguments.ToArray());
        }

        public static byte[] FormatPacket(int packetId, byte[] data)
        {
            byte[] packet = new byte[4 + 4 + data.Length];
            ByteUtil.InsertBytes(ref packet, 0, BitConverter.GetBytes(packetId));
            ByteUtil.InsertBytes(ref packet, 4, BitConverter.GetBytes(data.Length));
            ByteUtil.InsertBytes(ref packet, 8, data);
            return packet;
        }
        public static byte[] FormatPacket(Packet.P5_PACKET packetId, byte[] data)
        {
            return FormatPacket((int)packetId, data);
        }
        public static byte[] FormatPacket(int packetId, List<byte[]> args)
        {
            MemoryStream dataToBePacked = new MemoryStream();
            foreach (byte[] arg in args)
            {
                dataToBePacked.Write(arg);
            }
            return FormatPacket(packetId, dataToBePacked.ToArray());
        }
        public static byte[] FormatPacket(Packet.P5_PACKET packetId, List<byte[]> args)
        {
            return FormatPacket((int)packetId, args);
        }
    }
}
