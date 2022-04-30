namespace TrafficManager.API.Traffic.Enums {
    using System;
    using System.Linq;
    using System.Reflection;

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class LocaleKeyAttribute : Attribute {
        public string Key { get; private set; }

        public LocaleKeyAttribute(string key) => Key = key;

        private static MemberInfo ToEnumMember<TEnum>(string name) =>
            typeof(TEnum).GetMember(name).Single();

        public static string GetKey(MemberInfo info) {
            LocaleKeyAttribute a = info.GetCustomAttributes(typeof(LocaleKeyAttribute), false).FirstOrDefault() as LocaleKeyAttribute;
            return a?.Key;
        }

        public static string GetKey<TEnum>(string name) => GetKey(ToEnumMember<TEnum>(name)) ?? name;
    }
}
