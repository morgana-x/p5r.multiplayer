using P5R_MP_SERVER;
using p5rpc.lib.interfaces;
using Reloaded.Hooks.Definitions;
using Reloaded.Mod.Interfaces;
using Shared;
using System.Numerics;

namespace p5r.code.multiplayerclient.Components
{
    internal class NpcManager
    {
        public IP5RLib _p5rLib;

        ILogger _logger;

        Multiplayer multiplayer;

        P5RNative _p5rNative;
        // Should used NetworkedPlayer class here but I'm lazy af
        // Also like a neat small dictionary anyway (Don't currently have support for networking names / steamids and other junk)

        public Dictionary<int, int> playerNpcList = new Dictionary<int, int>();

        public int[] CurrentField = new int[] { -1, -1, 0 };
        public NpcManager(IP5RLib p5rlib, ILogger logger, IReloadedHooks hooks, Multiplayer multiplayer)
        {
            _p5rLib = p5rlib;
            _logger = logger;
            this.multiplayer = multiplayer;
            _p5rNative = new P5RNative(hooks, logger);
        }
        public int lastPcHandle = -1;

        private void OnFieldChange()
        {
            playerNpcList.Clear();
            foreach (var pair in multiplayer.PlayerList)
            {
                if (pair.Value.Field.SequenceEqual(CurrentField) && !isLoading() && CurrentField[0] != -1 && CurrentField[2] != -1)
                {
                    pair.Value.RefreshModel = true;
                    pair.Value.RefreshPosition = true;
                    pair.Value.RefreshRotation = true;
                }
            }
        }
        private bool isLoading()
        {
            return (!_p5rLib.FlowCaller.Ready() || CurrentField[0] == -1 || CurrentField[1] == -1 || lastPcHandle == -1);
        }
        public void MP_PLAYER_SET_FIELD(int netId, int[] field)
        {
            if (field != CurrentField)
            {
                MP_REMOVE_PLAYER(netId);
                return;
            }
            MP_SPAWN_PLAYER(netId);
        }

        public void MP_SPAWN_PLAYER(int netId, int modelMajor=1, int modelMinor=1, int modelSub=0)
        {
            if (isLoading()) return;
            int npcHandle = NPC_SPAWN(modelMajor, modelMinor, modelSub);
            if (npcHandle == -1)
                return;
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
                return;
            if (isLoading())
                return;
            NPC_DESPAWN(playerNpcList[netId]);
            playerNpcList.Remove(netId);
        }
        public void MP_SYNC_PLAYER_POS(int netid, float[] pos)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            if (isLoading())
                return;
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
                return;
                //MP_SPAWN_PLAYER(netid);
            NPC_SET_POS(playerNpcList[netid], pos);
        }
        public void MP_SYNC_PLAYER_ROT(int netid, float[] rot)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            if (isLoading())
                return;
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
                return;
                //MP_SPAWN_PLAYER(netid);
            NPC_SET_ROT(playerNpcList[netid], rot);
        }
        public void MP_SYNC_PLAYER_POS_DEST(int netid, float[] pos)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return;
            if (isLoading())
                return;
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
                return;
            _p5rLib.FlowCaller.FLD_MODEL_RUN_TRANSLATE(playerNpcList[netid], pos[0], pos[1], pos[2]);
        }
        public void MP_SYNC_PLAYER_MODEL(int netId, int modelIdMajor, int modelIdMinor, int modelIdSub)
        {
            MP_REMOVE_PLAYER(netId);
            MP_SPAWN_PLAYER(netId, modelIdMajor, modelIdMinor, modelIdSub);
        }

        public void MP_SYNC_PLAYER_ANIMATION(int netid, int animationId, int shouldLoop = -1)
        {
            if (!playerNpcList.ContainsKey(netid) || playerNpcList[netid] == -1)
                MP_SPAWN_PLAYER(netid);
            if (shouldLoop == -1)
                shouldLoop = 1;
            NPC_SET_ANIM(playerNpcList[netid], animationId, shouldLoop);
        }   
        public int[] GET_FIELD()
        {
            if (!_p5rLib.FlowCaller.Ready() || lastPcHandle == -1)
                return new int[3] { -1, -1, 0 };
            int fieldMajor = _p5rLib.FlowCaller.FLD_GET_MAJOR();
            int fieldMinor = _p5rLib.FlowCaller.FLD_GET_MINOR();
            int fieldSub   = _p5rLib.FlowCaller.FLD_GET_DIV_INDEX();

            return new int[3] { fieldMajor, fieldMinor, fieldSub };
        }
        public float[] PC_GET_POS(int pcHandle)
        {
            if (pcHandle == -1)
                return new float[3] { 0, 0, 0 };
            if (isLoading())
                return new float[3] { 0, 0, 0 };
            //return _p5rNative.GetModelPositionFromHandle(pcHandle);
            float x = _p5rLib.FlowCaller.FLD_MODEL_GET_X_TRANSLATE(pcHandle);
            float y = _p5rLib.FlowCaller.FLD_MODEL_GET_Y_TRANSLATE(pcHandle);
            float z = _p5rLib.FlowCaller.FLD_MODEL_GET_Z_TRANSLATE(pcHandle);
            return new float[3] { x, y, z };
        }

        public float[] PC_GET_ROT(int pcHandle)
        {
            if (pcHandle == -1)
                return new float[3] { 0, 0, 0 };
            if (isLoading())
                return new float[3] { 0, 0, 0 }; ;
            float xr = _p5rLib.FlowCaller.FLD_MODEL_GET_X_ROTATE(pcHandle);
            float yr = _p5rLib.FlowCaller.FLD_MODEL_GET_Y_ROTATE(pcHandle);
            float zr = _p5rLib.FlowCaller.FLD_MODEL_GET_Z_ROTATE(pcHandle);
            return new float[3] { xr, yr, zr };
        }
        public int PC_GET_HANDLE()
        {
            if (!_p5rLib.FlowCaller.Ready())//|| CurrentField[0] == -1 || CurrentField[1] == -1)
                return -1;
            lastPcHandle = _p5rLib.FlowCaller.FLD_PC_GET_RESHND(0);
            return lastPcHandle;
        }
        public int PC_GET_ANIM(int pcHandle)
        {
            if (isLoading())
                return -1;
            return NPC_GET_ANIM(pcHandle);

        }
        static int[] DefaultModel = new int[3] { 1, 1, 0 };
        public int[] PC_GET_MODEL(int pcHandle)
        {
            if (pcHandle == -1)
                return DefaultModel;
            if (isLoading())
                return DefaultModel;
            int modelMajor = _p5rLib.FlowCaller.MDL_GET_MAJOR_ID(pcHandle);
            int modelMinor = _p5rLib.FlowCaller.MDL_GET_MINOR_ID(pcHandle);
            int modelSub   = _p5rLib.FlowCaller.MDL_GET_SUB_ID(pcHandle);

            return new int[3] { modelMajor, modelMinor, modelSub };
        }
        public int NPC_SPAWN(int modelIdMajor, int modelIdMinor, int modelIdSub)
        {
            if (!_p5rLib.FlowCaller.Ready())
                return -1;
            if (isLoading())
                return -1;
            int npcHandle = _p5rLib.FlowCaller.FLD_NPC_MODEL_LOAD(modelIdMajor, modelIdMinor, modelIdSub);
            _p5rLib.FlowCaller.FLD_MODEL_LOADSYNC(npcHandle);
            _p5rLib.FlowCaller.FLD_MODEL_SET_VISIBLE(npcHandle, 1, 0);
            return npcHandle;
        }
        public void NPC_DESPAWN(int npcHandle)
        {
            if (npcHandle == -1)
                return;
            if (isLoading())
                return;
            _p5rLib.FlowCaller.FLD_MODEL_FREE(npcHandle);
        }
        public void NPC_SET_POS(int npcHandle, float[] pos)
        {
            if (isLoading())
                return;
            _p5rLib.FlowCaller.FLD_MODEL_SET_TRANSLATE(npcHandle, pos[0], pos[1], pos[2], 0);
        }
        public void NPC_SET_ROT(int npcHandle, float[] rot)
        {
            if (isLoading())
                return;
            _p5rLib.FlowCaller.FLD_MODEL_SET_ROTATE(npcHandle, rot[0], rot[1], rot[2], 0);
        }

        public void NPC_SET_ANIM(int npcHandle, int animationId, int gNpcAnimShouldLoop = 0)// int npcHandle, int gNpcAnimGapIndex, int gNpcGAPMinorId, int gNpcAnimShouldLoop=0)
        {
            if (animationId == -1)
                return;
            if (npcHandle == -1)
                return;
            if (isLoading())
                return;
            if (animationId < 0)
                animationId = 0;
            if (animationId == 58)
                animationId = 0;
            if (animationId == 0)
                gNpcAnimShouldLoop = 0;
            //int clone = _p5rLib.FlowCaller.FLD_MODEL_CLONE_ADDMOTION(npcHandle, 1);//gNpcGAPMinorId);
            //_p5rLib.FlowCaller.FLD_UNIT_WAIT_DISABLE(clone);
            _p5rLib.FlowCaller.MDL_ANIM(npcHandle, animationId, gNpcAnimShouldLoop, 0, 1);
            
            /*WAIT(gNpcAnimTime);
            //FLD_MODEL_REVERT_ADDMOTION(lastSpawnedNpcModelHandle, clone);*/

        }
        public int NPC_GET_ANIM(int npcHandle)
        {
            if (isLoading())
                return 0;
            int anim = _p5rLib.FlowCaller.MDL_GET_ANIM(npcHandle);
            return anim;
        }
        public bool FIELD_CHECK_CHANGE()
        {
            if (!_p5rLib.FlowCaller.Ready() || PC_GET_HANDLE() == -1)
                return false;
            int[] newField = GET_FIELD();
            if (newField.SequenceEqual(CurrentField))
                return false;
            OnFieldChange();
            CurrentField = newField;
            return true;
        }
    }
}
