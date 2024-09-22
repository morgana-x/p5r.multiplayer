namespace Shared
{
    internal class ByteUtil
    {
        public static void InsertBytes(ref byte[] destination, int index, byte[] appendage)
        {
            Buffer.BlockCopy(appendage, 0, destination, index, appendage.Length); ;
        }
        public static void InsertBytes(ref byte[] destination, int destIndex, int sourceIndex, byte[] appendage)
        {
            Buffer.BlockCopy(appendage, sourceIndex, destination, destIndex, appendage.Length-sourceIndex); ;
        }
    }
}
