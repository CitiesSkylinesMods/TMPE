namespace TrafficManager.API.Traffic.Enums {
    using System;
    using System.Linq;
    using System.Reflection;

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class KeyAttribute : Attribute {
        public string Key { get; private set; }

        public KeyAttribute(string key) => Key = key;

        private static MemberInfo ToEnumMember<TEnum>(string name) =>
            typeof(TEnum).GetMember(name).Single();

        public static string GetKey(MemberInfo info) {
            KeyAttribute a = info.GetCustomAttributes(typeof(KeyAttribute), false).FirstOrDefault() as KeyAttribute;
            return a?.Key;
        }

        public static string GetKey<TEnum>(string name) => GetKey(ToEnumMember<TEnum>(name)) ?? name;
    }
}
