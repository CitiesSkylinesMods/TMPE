namespace TrafficManager.UI.Helpers {
    using HarmonyLib;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text;

    [AttributeUsage(AttributeTargets.Enum, AllowMultiple = false)]
    public class KeyAttribute : Attribute {
        public string Key { get; private set; }

        public KeyAttribute(string key) => Key = key;

        public static MemberInfo ToEnumMember<T>(string name) =>
            typeof(T).GetMember(name, AccessTools.all).Single();

        public static string GetKey(MemberInfo info) {
            KeyAttribute a = info.GetCustomAttributes(typeof(KeyAttribute), false).FirstOrDefault() as KeyAttribute;
            return a?.Key;
        }

        public static string GetKey<T>(string name) =>
            GetKey(ToEnumMember<T>(name)) ?? name;
    }
}
