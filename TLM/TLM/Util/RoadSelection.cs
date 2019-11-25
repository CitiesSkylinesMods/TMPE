using System;
using ColossalFramework;
using System.Reflection;

namespace TrafficManager.Util {
    public class RoadSelection {
        public static FastList<ushort> GetPath() =>
            (FastList<ushort>)GetField(Singleton<NetManager>.instance.NetAdjust, "m_tempPath");

        public static object GetField(object instance, string field_name) {
            Type type = instance.GetType();
            FieldInfo field = type.GetField(field_name, BindingFlags.Instance | BindingFlags.NonPublic);
            return field.GetValue(instance);
        }

        public static string PrintPath() {
            FastList<ushort> path = GetPath();
            string ret = $"path[{path?.m_size}] = ";
            for (int i = 0; i < path?.m_size; ++i) {
                ushort segID = path.m_buffer[i];
                ret += $"{segID} ";
            }
            return ret;
        }
    }
}
