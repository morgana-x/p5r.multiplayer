
using System.Runtime.InteropServices;

namespace p5r.code.multiplayerclient.Utility
{
    public class Input
    {
        [DllImport("User32.dll")]
        private static extern short GetKeyState(int vKey);
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        public static bool IsKeydown(int vKey)
        {
            return ((ushort)GetKeyState(vKey) >> 15) == 1;
        }
    }
}
