using p5rpc.lib.interfaces;
using Reloaded.Mod.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace p5r.code.multiplayerclient.Components
{
    internal class NpcManager
    {
        public IP5RLib _p5rLib;

        ILogger _logger;

        // Should used NetworkedPlayer class here but I'm lazy af
        // Also like a neat small dictionary anyway (Don't currently have support for networking names / steamids and other junk)

        public Dictionary<int, int> playerNpcList = new Dictionary<int, int>();
       // public Dictionary<int, int[]> playerFieldList = new Dictionary<int, int[]>();

        public int[] CurrentField = new int[] { 0, 0 };
        public NpcManager(IP5RLib p5rlib, ILogger logger)
        {
            _p5rLib = p5rlib;
            _logger = logger;
           // Task.Run(Run);
        }

        private void OnFieldChange()
        {
            playerNpcList.Clear(); // Obviously these handles won't be accurate anymore!
        }
        public void MP_PLAYER_SET_FIELD(int netId, int[] field)
        {
            //if (!playerFieldList.ContainsKey(netId))
            //    playerFieldList.Add(netId, field);
            //playerFieldList[netId] = field;
            if (field == CurrentField)
            {
                MP_SPAWN_PLAYER(netId);
            }

        }

        public void MP_SPAWN_PLAYER(int netId, int modelMajor=1, int modelMinor=1, int modelSub=0)
        {
            
            int npcHandle = NPC_SPAWN(modelMajor, modelMinor, modelSub);
            if (npcHandle == -1)
            {
                return;
            }
            if (!playerNpcList.ContainsKey(netId))
            {
                playerNpcList.Add(netId, npcHandle);
                return;
            }
            playerNpcList[netId] = npcHandle;
        }
        public void MP_SPAWN_PLAYER(int netId, int[] model)
        {
            MP_SPAWN_PLAYER(netId, model[0], model[1], model[2]);
        }
        
        public void MP_REMOVE_PLAYER(int netId)
        {
            if (!playerNpcList.ContainsKey(netId))
            {
                return;
            }
            NPC_DESPAWN(playerNpcList[netId]);
            playerNpcList.Remove(netId);
        }
        public void MP_SYNC_PLAYER_POS(int netid, float[] pos)
        {
            if (!_p5rLib.FlowCaller.Ready())
            {
                return;
            }
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
            {
                MP_SPAWN_PLAYER(netid);
            }
            NPC_SET_POS(playerNpcList[netid], pos);
        }
        public void MP_SYNC_PLAYER_ROT(int netid, float[] rot)
        {
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
            {
                MP_SPAWN_PLAYER(netid);
            }
            NPC_SET_ROT(playerNpcList[netid], rot);
        }
        public void MP_SYNC_PLAYER_MODEL(int netId, int modelIdMajor, int modelIdMinor, int modelIdSub)
        {
            MP_REMOVE_PLAYER(netId);
            MP_SPAWN_PLAYER(netId, modelIdMajor, modelIdMinor, modelIdSub);
        }

        public void MP_SYNC_PLAYER_ANIMATION(int netid, int gNpcAnimGapIndex, int gNpcAnimGapMinorId, int gNpcAnimShouldLoop)
        {
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
            {
                MP_SPAWN_PLAYER(netid);
            }
            NPC_SET_ANIM(playerNpcList[netid], gNpcAnimGapIndex, gNpcAnimGapMinorId, gNpcAnimShouldLoop);
        }
        public int[] GET_FIELD()
        {
            if (!_p5rLib.FlowCaller.Ready())
            {
                return new int[2] { 0, 0 };
            }
            int fieldMajor = _p5rLib.FlowCaller.FLD_GET_MAJOR();
            int fieldMinor = _p5rLib.FlowCaller.FLD_GET_MINOR();

            return new int[2] { fieldMajor, fieldMinor };
        }
        public float[] PC_GET_POS(int pcHandle)
        {
            if (!_p5rLib.FlowCaller.Ready())
            {
                return new float[3] { 0, 0, 0 };
            }
            float x = _p5rLib.FlowCaller.FLD_MODEL_GET_X_TRANSLATE(pcHandle);
            float y = _p5rLib.FlowCaller.FLD_MODEL_GET_Y_TRANSLATE(pcHandle);
            float z = _p5rLib.FlowCaller.FLD_MODEL_GET_Z_TRANSLATE(pcHandle);
            return new float[3] { x, y, z };
        }

        public float[] PC_GET_ROT(int pcHandle)
        {
            if (!_p5rLib.FlowCaller.Ready())
            {
                return new float[3] { 0, 0, 0 };
            }
            float xr = _p5rLib.FlowCaller.FLD_MODEL_GET_X_ROTATE(pcHandle);
            float yr = _p5rLib.FlowCaller.FLD_MODEL_GET_Y_ROTATE(pcHandle);
            float zr = _p5rLib.FlowCaller.FLD_MODEL_GET_Z_ROTATE(pcHandle);
            return new float[3] { xr, yr, zr };
        }
        public int PC_GET_HANDLE()
        {
            if (!_p5rLib.FlowCaller.Ready())
                return -1;
            return _p5rLib.FlowCaller.FLD_PC_GET_RESHND(0);
        }
        static int[] DefaultModel = new int[3] { 1, 1, 0 };
        public int[] PC_GET_MODEL(int pcHandle)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return DefaultModel;
            if (pcHandle == -1)
                return DefaultModel;
            int modelMajor = _p5rLib.FlowCaller.MDL_GET_MAJOR_ID(pcHandle);
            int modelMinor = _p5rLib.FlowCaller.MDL_GET_MINOR_ID(pcHandle);
            int modelSub = _p5rLib.FlowCaller.MDL_GET_SUB_ID(pcHandle);

            return new int[3] { modelMajor, modelMinor, modelSub };
        }
        public int NPC_SPAWN(int modelIdMajor, int modelIdMinor, int modelIdSub)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return -1;
            int npcHandle = _p5rLib.FlowCaller.FLD_NPC_MODEL_LOAD(modelIdMajor, modelIdMinor, modelIdSub);
            _p5rLib.FlowCaller.FLD_MODEL_LOADSYNC(npcHandle);
            _p5rLib.FlowCaller.FLD_MODEL_SET_VISIBLE(npcHandle, 1, 0);
            return npcHandle;
        }
        public void NPC_DESPAWN(int npcHandle)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            if (npcHandle == -1)
                return;
            _p5rLib.FlowCaller.FLD_MODEL_FREE(npcHandle);
        }
        public void NPC_SET_POS(int npcHandle, float[] pos)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            _p5rLib.FlowCaller.FLD_MODEL_SET_TRANSLATE(npcHandle, pos[0], pos[1], pos[2], 0);
        }
        public void NPC_SET_ROT(int npcHandle, float[] rot)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            _p5rLib.FlowCaller.FLD_MODEL_SET_ROTATE(npcHandle, rot[0], rot[1], rot[2], 0);
        }

        public void NPC_SET_ANIM(int npcHandle, int gNpcAnimGapIndex, int gNpcGAPMinorId, int gNpcAnimShouldLoop=0)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            int clone = _p5rLib.FlowCaller.FLD_MODEL_CLONE_ADDMOTION(npcHandle, gNpcGAPMinorId);
            _p5rLib.FlowCaller.FLD_UNIT_WAIT_DISABLE(clone);
            _p5rLib.FlowCaller.MDL_ANIM(npcHandle, gNpcAnimGapIndex, gNpcAnimShouldLoop, 0, 1);
            /*WAIT(gNpcAnimTime);
            //FLD_MODEL_REVERT_ADDMOTION(lastSpawnedNpcModelHandle, clone);*/

        }
        public int NPC_GET_ANIM(int npcHandle)
        {
            int anim = _p5rLib.FlowCaller.MDL_GET_ANIM(npcHandle);
            return anim;
        }
        public bool FIELD_CHECK_CHANGE()
        {
            if (!_p5rLib.FlowCaller.Ready())
                return false;
            int fieldMajor = _p5rLib.FlowCaller.FLD_GET_MAJOR();
            int fieldMinor = _p5rLib.FlowCaller.FLD_GET_MINOR();
            int[] newField = new int[] { fieldMajor, fieldMinor };

            bool changed = false;
            if (!newField.SequenceEqual(CurrentField))
            {
                OnFieldChange();
                changed = true;
            }
            CurrentField = newField;
            return changed;
        }

     
  
    }
}
