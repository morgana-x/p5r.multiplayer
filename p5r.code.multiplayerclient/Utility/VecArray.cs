using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p5r.code.multiplayerclient.Utility
{
    internal class VecArray
    {
        public static float Dist(float[] pos1, float[] pos2)
        {
            float dx = pos1[0] - pos2[0];
            float dy = pos1[1] - pos2[1];
            float dz = pos1[2] - pos2[2];
            return (float)Math.Sqrt((dx * dx) + (dy * dy) + (dz * dy));
        }
        public static float[] Middle(float[] pos1, float[] pos2)
        {
            return new float[] { pos1[0] - pos2[0], pos1[1] - pos2[1], pos1[2] - pos2[2] };
        }
        static float lerp(float v0, float v1, float t)
        {
            if (t > 0.9)
                return v0;
            return v0 + t * (v1 - v0);
        }
        public static float[] Lerp(float[] pos1, float[] pos2, float t)
        {
            return new float[] { lerp(pos1[0], pos2[0], t), lerp(pos1[1], pos2[1], t), lerp(pos1[2], pos2[2], t) };
        }
    }
}
