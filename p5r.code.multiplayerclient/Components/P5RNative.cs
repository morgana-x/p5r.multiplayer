using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.X64;
using Reloaded.Memory.SigScan.ReloadedII.Interfaces;
using System.Runtime.InteropServices;
using System.Diagnostics;
using p5r.code.multiplayerclient.Utility;
using Reloaded.Mod.Interfaces;
using static System.Runtime.InteropServices.JavaScript.JSType;
namespace p5r.code.multiplayerclient.Components
{
    public class P5RNative
    {
        static string Get_Model_Data_Sig = "48 8b d1 48 85 c9 74 2f 48 8b 49 10 0f b6 c1 c0 e8 05 a8 01 75 21 4c 8b 02 49 c1 e8 3a 41 83 f8 07";
       // static string Get_Field_Data_Sig = "40 53 48 83 ec 20 48 8b 1d 8b 37 53 f3 48 85 db 0f 84 ab 00 00 00 48 8b 0d 73 37 53 f3 48 85 c9 74 36";
        long fieldModelListOffset = 0x1429048c0;
        Process proc;
        GetModelData getModelData;
        //GetFieldData getFieldData;
        ILogger _logger;

        long field_major_id_offset = 0x2855A34;
        long baseAddr = 0;
        public P5RNative(IReloadedHooks hooks, ILogger logger)
        {
            _logger = logger;
            proc = Process.GetCurrentProcess();
            baseAddr = MemoryRead.GetProcessBaseAddress(proc);
            Utils.SigScan(Get_Model_Data_Sig, "FlowGetModelData", address =>
            {
                getModelData = hooks.CreateWrapper<GetModelData>(address, out _);
            });
            /*Utils.SigScan(Get_Field_Data_Sig, "GetFieldData", address =>
            {
               // Console.WriteLine("Found address for getfielddata at" + address.ToString("X"));
                getFieldData = hooks.CreateWrapper<GetFieldData>(address, out _);
            });*/
        }

        public int FLD_GET_MAJOR()
        {
            return MemoryRead.ReadInt((int)proc.Handle, baseAddr + field_major_id_offset);
            /*
            long data = getFieldData.Invoke();
            Console.WriteLine("Data: " + data.ToString("X"));
            if (data == 0)
                return 0;

            long data2 = MemoryRead.ReadLong((int)proc.Handle, data + 0x48);
            Console.WriteLine("Data2: " + data2.ToString("X"));
            if (data2 == 0)
                return 0;

            var data3 = (uint)MemoryRead.ReadShort((int)proc.Handle, data2 + 0x1fa);
            Console.WriteLine("Data3: " + data3.ToString("X"));
            return (int)data3;*/
        }

        public float[] GetModelPositionFromHandle(int handle)
        {
            long data = GetModelDataFromHandle(handle);
            float[] position = new float[3];
            position[0] = MemoryRead.ReadFloat((int)proc.Handle, data + 4);
            position[1] = MemoryRead.ReadFloat((int)proc.Handle, data + 8);
            position[2] = MemoryRead.ReadFloat((int)proc.Handle, data + 12);
            Console.WriteLine(string.Join(",", position));
            return position;
        }

        public int MDL_GET_MINOR_ID(int modelHandle)
        {

            long mdlListOffset = fieldModelListOffset;
            while (MemoryRead.ReadLong((int)proc.Handle, mdlListOffset) < 0x1429049c0)
            {
                for (long foundEntIndex = mdlListOffset; foundEntIndex != 0;
                    foundEntIndex = MemoryRead.ReadLong((int)proc.Handle, foundEntIndex + 0x2c8))
                {
                    if (MemoryRead.ReadInt((int)proc.Handle, foundEntIndex + 8) != modelHandle)
                        continue;
                    if (foundEntIndex == 0)
                        continue;

                    Console.WriteLine("Found " + modelHandle);
                    long modelId = foundEntIndex >> 0xc;

                    return (int)modelId;
                }
                mdlListOffset++;
            }
            return -1;
        }
        public long GetModelDataFromHandle(int modelHandle)
        {
            if (modelHandle < 0)
            {
                return 0;
            }
  
            long mdlListOffset = fieldModelListOffset;
            while (MemoryRead.ReadLong((int)proc.Handle, mdlListOffset)< 0x1429049c0)
            {
                for (long foundEntIndex = MemoryRead.ReadLong((int)proc.Handle,
                    mdlListOffset); foundEntIndex !=0; 
                    foundEntIndex = MemoryRead.ReadLong((int)proc.Handle,foundEntIndex + 0x2c8))
                {
                    if (MemoryRead.ReadInt((int)proc.Handle, foundEntIndex + 8) != modelHandle)
                        continue;
                    if (foundEntIndex == 0)
                        continue;
                    Console.WriteLine("Found " + modelHandle);
                    long modelData = getModelData.Invoke(foundEntIndex);  // = GetModelData(foundEndIndex);
                    return modelData;
                }
                mdlListOffset += 1;
            }
            return 0;
        }

        [Function(CallingConventions.Microsoft)]
        private delegate long GetModelData(long entIndex);

        [Function(CallingConventions.Microsoft)]
        private delegate long GetFieldData();
    }
}
