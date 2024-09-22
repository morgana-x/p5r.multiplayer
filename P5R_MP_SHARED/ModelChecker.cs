using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P5R_MP_SERVER
{
    public class ModelChecker
    {
        static int[] ValidJokerModelMinorIds = new int[]
        {
            1,
            2,
            3,
            4,
            5,
            6,
           // 48, title screen model let's not
            51,
            52,
            61,
            63,
            64,
            65,
            66,
            67,
            69, // Why is this the swim suit model @Atlus Employee
            71,
            72,
            73,
            99,
            101,
            102,
            103,
            104,
            106,
            107,
            110,
            111,
            112,
            114,
            116,
            117,
           // 118,
           // 119,
            151,
            152,
            153,
            154,
            155,
            156,
            157,
            158,
            159,
            160,
            161,
            162,
            163,
            164,
            165,
            166,
            167,
            168,
            169,
            170,
            171,
            172,
            173,
            174,
            175,
            176,
            177,
            201, // sub id must be 3 or 4
        };
        public static int[][] ValidModels = new int[][]
        {
            new int[]{1,1,0},
            new int[]{1,2,0},
            new int[]{1,3,0},
            new int[]{1,4,0},
            new int[]{1,5,0},
            new int[]{1,6,0},
            //new int[]{1,48,0}, Titlescreen model
            new int[]{1,51,0},
            new int[]{1,52,0},
            new int[]{1,61,0},
            new int[]{1,63,0},
            new int[]{1,64,0},
            new int[]{1,65,0},
            new int[]{1,66,0},
            new int[]{1,67,0},
            new int[]{1,69,0},
            new int[]{1,71,0},
            new int[]{1,72,0},
            new int[]{1,73,0},
            new int[]{1,99,0},
            new int[]{1,101,0},
            new int[]{1,102,0},
            new int[]{1,103,0},
            new int[]{1,104,0},
            new int[]{1,106,0},
            new int[]{1,107,0},
            new int[]{1,110,0},
            new int[]{1,111,0},
            new int[]{1,112,0},
            new int[]{1,114,0},
            new int[]{1,116,0},
            new int[]{1,117,0},
            //new int[]{1,118
            //new int[]{1,119,0},
            new int[]{1,151,0},
            new int[]{1,152,0 },
            new int[]{1,153,0 },
            new int[]{1,154,0},
            new int[]{1,155,0 },
            new int[]{1,156,0 },
            new int[]{1,157,0 },
            new int[]{1,158,0 },
            new int[]{1,159,0 },
            new int[]{1,160,0 },
            new int[]{1,161,0 },
            new int[]{1,162,0 },
            new int[]{1,163,0 },
            new int[]{1,164,0 },
            new int[]{1,165,0 },
            new int[]{1,166,0 },
            new int[]{1,167,0 },
            new int[]{1,168,0 },
            new int[]{1,169,0 },
            new int[]{1,170,0 },
            new int[]{1,171,0 },
            new int[]{1,172,0 },
            new int[]{1,173,0 },
            new int[]{1,174,0 },
            new int[]{1,175,0 },
            new int[]{1,176,0 },
            new int[]{1,177,0 },
            new int[]{1,201,3 },
            new int[]{1,201,4 },
        };

        public static int GetModelId(int[] model)
        {
            for (int i = 0; i < ValidModels.Length; i++)
            {
                if (ValidModels[i].SequenceEqual(model))
                {
                    return i;
                }
            }
            return 0;
        }
        public static int[] GetModelFromId(int modelId)
        {
            if (modelId >= ValidModels.Length)
                return ValidModels[0];
            if (modelId < 0)
                return ValidModels[0];
            return ValidModels[modelId];
        }
        public static bool IsValidJokerModel(int[] model)
        {
            if (model[0] != 1)
                return false;
            if (model[1] < 1)
                return false;
            if ((model[1] == 201 && (model[2] == 3 || model[2] == 4)))
                return true;
            if (model[2] != 0)
                return false;
            if (!ValidJokerModelMinorIds.Contains(model[1]))
                return false;
            return true;
        }
        
    }
}
