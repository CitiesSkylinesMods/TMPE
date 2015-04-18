using System;
using System.Collections.Generic;
using System.Text;
using ColossalFramework;
using UnityEngine;

namespace TrafficManager
{
    class CustomNetNode
    {
        public void RefreshJunctionData(ushort nodeID, int segmentIndex, ushort nodeSegment, Vector3 centerPos, ref uint instanceIndex, ref RenderManager.Instance data)
        {
            NetNode thisNode = NetManager.instance.m_nodes.m_buffer[nodeID];
            NetManager instance = Singleton<NetManager>.instance;
            data.m_position = thisNode.m_position;
            data.m_rotation = Quaternion.identity;
            data.m_initialized = true;
            float vScale = 0.05f;
            Vector3 zero = Vector3.zero;
            Vector3 zero2 = Vector3.zero;
            Vector3 zero3 = Vector3.zero;
            Vector3 zero4 = Vector3.zero;
            Vector3 vector = Vector3.zero;
            Vector3 vector2 = Vector3.zero;
            Vector3 a = Vector3.zero;
            Vector3 a2 = Vector3.zero;
            Vector3 zero5 = Vector3.zero;
            Vector3 zero6 = Vector3.zero;
            Vector3 zero7 = Vector3.zero;
            Vector3 zero8 = Vector3.zero;
            NetSegment netSegment = instance.m_segments.m_buffer[(int)nodeSegment];
            NetInfo info = netSegment.Info;
            ItemClass connectionClass = info.GetConnectionClass();
            Vector3 vector3 = (nodeID != netSegment.m_startNode) ? netSegment.m_endDirection : netSegment.m_startDirection;
            float num = -4f;
            float num2 = -4f;
            ushort num3 = 0;
            ushort num4 = 0;
            for (int i = 0; i < 8; i++)
            {
                ushort segment = thisNode.GetSegment(i);
                if (segment != 0 && segment != nodeSegment)
                {
                    NetInfo info2 = instance.m_segments.m_buffer[(int)segment].Info;
                    ItemClass connectionClass2 = info2.GetConnectionClass();
                    if (connectionClass.m_service == connectionClass2.m_service)
                    {
                        NetSegment netSegment2 = instance.m_segments.m_buffer[(int)segment];
                        Vector3 vector4 = (nodeID != netSegment2.m_startNode) ? netSegment2.m_endDirection : netSegment2.m_startDirection;
                        float num5 = vector3.x * vector4.x + vector3.z * vector4.z;
                        if (vector4.z * vector3.x - vector4.x * vector3.z < 0f)
                        {
                            if (num5 > num)
                            {
                                num = num5;
                                num3 = segment;
                            }
                            num5 = -2f - num5;
                            if (num5 > num2)
                            {
                                num2 = num5;
                                num4 = segment;
                            }
                        }
                        else
                        {
                            if (num5 > num2)
                            {
                                num2 = num5;
                                num4 = segment;
                            }
                            num5 = -2f - num5;
                            if (num5 > num)
                            {
                                num = num5;
                                num3 = segment;
                            }
                        }
                    }
                }
            }
            bool start = netSegment.m_startNode == nodeID;
            bool flag;
            netSegment.CalculateCorner(nodeSegment, true, start, false, out zero, out zero3, out flag);
            netSegment.CalculateCorner(nodeSegment, true, start, true, out zero2, out zero4, out flag);
            if (num3 != 0 && num4 != 0)
            {

                float num6 = info.m_pavementWidth / info.m_halfWidth * 0.5f;
                float y = 1f;
                if (num3 != 0)
                {
                    NetSegment netSegment3 = instance.m_segments.m_buffer[(int)num3];
                    NetInfo info3 = netSegment3.Info;
                    start = (netSegment3.m_startNode == nodeID);
                    netSegment3.CalculateCorner(num3, true, start, true, out vector, out a, out flag);
                    netSegment3.CalculateCorner(num3, true, start, false, out vector2, out a2, out flag);
                    float num7 = info3.m_pavementWidth / info3.m_halfWidth * 0.5f;
                    num6 = (num6 + num7) * 0.5f;
                    y = 2f * info.m_halfWidth / (info.m_halfWidth + info3.m_halfWidth);
                }
                float num8 = info.m_pavementWidth / info.m_halfWidth * 0.5f;
                float w = 1f;
                if (num4 != 0)
                {
                    NetSegment netSegment4 = instance.m_segments.m_buffer[(int)num4];
                    NetInfo info4 = netSegment4.Info;
                    start = (netSegment4.m_startNode == nodeID);
                    netSegment4.CalculateCorner(num4, true, start, true, out zero5, out zero7, out flag);
                    netSegment4.CalculateCorner(num4, true, start, false, out zero6, out zero8, out flag);
                    float num9 = info4.m_pavementWidth / info4.m_halfWidth * 0.5f;
                    num8 = (num8 + num9) * 0.5f;
                    w = 2f * info.m_halfWidth / (info.m_halfWidth + info4.m_halfWidth);
                }
                Vector3 vector5;
                Vector3 vector6;
                NetSegment.CalculateMiddlePoints(zero, -zero3, vector, -a, true, true, out vector5, out vector6);
                Vector3 vector7;
                Vector3 vector8;
                NetSegment.CalculateMiddlePoints(zero2, -zero4, vector2, -a2, true, true, out vector7, out vector8);
                Vector3 vector9;
                Vector3 vector10;
                NetSegment.CalculateMiddlePoints(zero, -zero3, zero5, -zero7, true, true, out vector9, out vector10);
                Vector3 vector11;
                Vector3 vector12;
                NetSegment.CalculateMiddlePoints(zero2, -zero4, zero6, -zero8, true, true, out vector11, out vector12);

                data.m_dataMatrix0 = NetSegment.CalculateControlMatrix(zero, vector5, vector6, vector, zero, vector5, vector6, vector, thisNode.m_position, vScale);
                data.m_extraData.m_dataMatrix2 = NetSegment.CalculateControlMatrix(zero2, vector7, vector8, vector2, zero2, vector7, vector8, vector2, thisNode.m_position, vScale);
                data.m_extraData.m_dataMatrix3 = NetSegment.CalculateControlMatrix(zero, vector9, vector10, zero5, zero, vector9, vector10, zero5, thisNode.m_position, vScale);
                data.m_dataMatrix1 = NetSegment.CalculateControlMatrix(zero2, vector11, vector12, zero6, zero2, vector11, vector12, zero6, thisNode.m_position, vScale);
                data.m_dataVector0 = new Vector4(0.5f / info.m_halfWidth, 1f / info.m_segmentLength, 0.5f - info.m_pavementWidth / info.m_halfWidth * 0.5f, info.m_pavementWidth / info.m_halfWidth * 0.5f);
                data.m_dataVector1 = centerPos - data.m_position;

                if ((thisNode.m_flags & NetNode.Flags.Junction) == NetNode.Flags.None)
                    data.m_dataVector1.w = (data.m_dataMatrix0.m33 + data.m_extraData.m_dataMatrix2.m33 + data.m_extraData.m_dataMatrix3.m33 + data.m_dataMatrix1.m33) * 0.25f;
                else
                    data.m_dataVector1.w = 0.01f;

                data.m_dataVector2 = new Vector4(num6, y, num8, w);
                data.m_extraData.m_dataVector4 = RenderManager.GetColorLocation(65536u + (uint)nodeID);
            }
            else
            {
                centerPos.x = (zero.x + zero2.x) * 0.5f;
                centerPos.z = (zero.z + zero2.z) * 0.5f;
                vector = zero2;
                vector2 = zero;
                a = zero4;
                a2 = zero3;
                float d = Mathf.Min(info.m_halfWidth * 1.33333337f, 16f);
                Vector3 vector13 = zero - zero3 * d;
                Vector3 vector14 = vector - a * d;
                Vector3 vector15 = zero2 - zero4 * d;
                Vector3 vector16 = vector2 - a2 * d;
                Vector3 vector17 = zero + zero3 * d;
                Vector3 vector18 = vector + a * d;
                Vector3 vector19 = zero2 + zero4 * d;
                Vector3 vector20 = vector2 + a2 * d;
                data.m_dataMatrix0 = NetSegment.CalculateControlMatrix(zero, vector13, vector14, vector, zero, vector13, vector14, vector, thisNode.m_position, vScale);
                data.m_extraData.m_dataMatrix2 = NetSegment.CalculateControlMatrix(zero2, vector19, vector20, vector2, zero2, vector19, vector20, vector2, thisNode.m_position, vScale);
                data.m_extraData.m_dataMatrix3 = NetSegment.CalculateControlMatrix(zero, vector17, vector18, vector, zero, vector17, vector18, vector, thisNode.m_position, vScale);
                data.m_dataMatrix1 = NetSegment.CalculateControlMatrix(zero2, vector15, vector16, vector2, zero2, vector15, vector16, vector2, thisNode.m_position, vScale);
                data.m_dataMatrix0.SetRow(3, data.m_dataMatrix0.GetRow(3) + new Vector4(0.2f, 0.2f, 0.2f, 0.2f));
                data.m_extraData.m_dataMatrix2.SetRow(3, data.m_extraData.m_dataMatrix2.GetRow(3) + new Vector4(0.2f, 0.2f, 0.2f, 0.2f));
                data.m_extraData.m_dataMatrix3.SetRow(3, data.m_extraData.m_dataMatrix3.GetRow(3) + new Vector4(0.2f, 0.2f, 0.2f, 0.2f));
                data.m_dataMatrix1.SetRow(3, data.m_dataMatrix1.GetRow(3) + new Vector4(0.2f, 0.2f, 0.2f, 0.2f));
                data.m_dataVector0 = new Vector4(0.5f / info.m_halfWidth, 1f / info.m_segmentLength, 0.5f - info.m_pavementWidth / info.m_halfWidth * 0.5f, info.m_pavementWidth / info.m_halfWidth * 0.5f);
                data.m_dataVector1 = centerPos - data.m_position;
                data.m_dataVector1.w = (data.m_dataMatrix0.m33 + data.m_extraData.m_dataMatrix2.m33 + data.m_extraData.m_dataMatrix3.m33 + data.m_dataMatrix1.m33) * 0.25f;
                data.m_dataVector2 = new Vector4(info.m_pavementWidth / info.m_halfWidth * 0.5f, 1f, info.m_pavementWidth / info.m_halfWidth * 0.5f, 1f);
                data.m_extraData.m_dataVector4 = RenderManager.GetColorLocation(65536u + (uint)nodeID);
            }
            data.m_dataInt0 = segmentIndex;
            data.m_dataColor0 = info.m_color;
            data.m_dataColor0.a = 0f;
            if (info.m_requireSurfaceMaps)
            {
                Singleton<TerrainManager>.instance.GetSurfaceMapping(data.m_position, out data.m_dataTexture0, out data.m_dataTexture1, out data.m_dataVector3);
            }
            instanceIndex = (uint)data.m_nextInstance;
        }
    }
}
