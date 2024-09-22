﻿
namespace P5R_MP_SERVER
{
    public static class Runtime // https://stackoverflow.com/questions/10161088/get-elapsed-time-since-application-start
    {
        static Runtime()
        {
            var ThisProcess = System.Diagnostics.Process.GetCurrentProcess(); LastSystemTime = (long)(System.DateTime.Now - ThisProcess.StartTime).TotalMilliseconds; ThisProcess.Dispose();
            StopWatch = new System.Diagnostics.Stopwatch(); StopWatch.Start();
        }
        private static long LastSystemTime;
        private static System.Diagnostics.Stopwatch StopWatch;

        //Public.
        public static long CurrentRuntime { get { return StopWatch.ElapsedMilliseconds + LastSystemTime; } }
    }
}
