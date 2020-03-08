namespace TrafficManager.Patch._NetNode {
    using Harmony;
    using System.Collections.Generic;
    using System.Reflection;
    using System.Reflection.Emit;
    using ColossalFramework;
    using static TranspilerUtils;
    using TrafficManager.Util;
    using TrafficManager.Util.Caching;

    public static class CheckTracksCommons {
        public static bool ShouldConnectTracks(
            ushort nodeId,
            int nodeInfoIDX,
            ref RenderManager.Instance data) {
            ushort sourceSegmentID = nodeId.ToNode().GetSegment(data.m_dataInt0 & 7);
            int targetSegmentIDX = data.m_dataInt0 >> 4;
            return DirectConnectCache.GetShouldConnectTracks(
                sourceSegmentID,
                targetSegmentIDX,
                nodeId,
                nodeInfoIDX);
        }

        static MethodInfo ShouldConnectTracks_ => typeof(CheckTracksCommons).GetMethod("ShouldConnectTracks");
        static MethodInfo CheckRenderDistance_ => typeof(RenderManager.CameraInfo).GetMethod("CheckRenderDistance");
        static FieldInfo  m_nodes_ => typeof(NetInfo).GetField("m_nodes");

        public static void ApplyCheckTracks(List<CodeInstruction> codes, MethodInfo method, int occurance) {
            Shortcuts.Assert(CheckRenderDistance_ != null, "mCheckRenderDistance!=null failed");
            Shortcuts.Assert(ShouldConnectTracks_ != null, "mShouldConnectTracks!=null failed");
            Shortcuts.Assert(ShouldConnectTracks_ != null, "mShouldConnectTracks!=null failed");

            /*
            --->insert here
            [164 17 - 164 95]
            IL_02c0: ldarg.1      // cameraInfo
            IL_02c1: ldarg.s      data
            IL_02c3: ldfld        valuetype [UnityEngine]UnityEngine.Vector3 RenderManager/Instance::m_position
            IL_02c8: ldloc.s      node // 
            IL_02ca: ldfld        float32 NetInfo/Node::m_lodRenderDistance
            IL_02cf: callvirt     instance bool RenderManager/CameraInfo::CheckRenderDistance(valuetype [UnityEngine]UnityEngine.Vector3, float32)
            IL_02d4: brfalse      IL_0405
             */

            CodeInstruction LDArg_NodeID = GetLDArg(method, "nodeID");
            CodeInstruction LDArg_data = GetLDArg(method, "data");

            int index = 0;
            index = SearchInstruction(codes, new CodeInstruction(OpCodes.Callvirt, CheckRenderDistance_), index, counter: occurance);
            Shortcuts.Assert(index != 0, "index!=0");
            CodeInstruction LDLoc_NodeInfoIDX = Search_LDLoc_NodeInfoIDX(codes, index, counter:1, dir:-1);

            //seek to <ldarg.s cameraInfo> instruction:
            index = SearchInstruction(codes, GetLDArg(method, "cameraInfo"), index, counter: occurance, dir:-1); 

            Label ContinueIndex = GetContinueLabel(codes, index, dir: -1); // IL_029d: br IL_0570
            {
                var newInstructions = new[]{
                    LDArg_NodeID, 
                    LDLoc_NodeInfoIDX, 
                    LDArg_data, 
                    new CodeInstruction(OpCodes.Call, ShouldConnectTracks_),
                    new CodeInstruction(OpCodes.Brfalse, ContinueIndex), // if returned value is false then continue to the next iteration of for loop;
                };

                InsertInstructions(codes, newInstructions, index, true);
            } // end block
        } // end method

        public static CodeInstruction Search_LDLoc_NodeInfoIDX(List<CodeInstruction> codes, int index, int counter , int dir) {
            Shortcuts.Assert(m_nodes_ != null, "f_m_nodes!=null failed");
            index = SearchInstruction(codes, new CodeInstruction(OpCodes.Ldfld, m_nodes_), index, counter: counter, dir: dir);

            var code = codes[index + 1];
            Shortcuts.Assert(IsLdLoc(code), $"IsLdLoc(code) | code={code}");
            return code;
            
        }



    }
}
