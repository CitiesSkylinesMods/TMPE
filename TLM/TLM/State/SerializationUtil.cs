namespace TrafficManager.State {
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Runtime.Serialization;
    using System.Runtime.Serialization.Formatters;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class SerializationUtil {
        public const BindingFlags COPYABLE = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        static BinaryFormatter GetBinaryFormatter =>
            new BinaryFormatter() { AssemblyFormat = FormatterAssemblyStyle.Simple };

        public static object Deserialize(byte[] data) {
            if (data == null)
                return null;

            var memoryStream = new MemoryStream();
            memoryStream.Write(data, 0, data.Length);
            memoryStream.Position = 0;
            return GetBinaryFormatter.Deserialize(memoryStream);
        }

        public static byte[] Serialize(object obj) {
            var memoryStream = new MemoryStream();
            GetBinaryFormatter.Serialize(memoryStream, obj);
            memoryStream.Position = 0; // redundant?
            return memoryStream.ToArray();
        }

        public static void GetObjectFields(this SerializationInfo info, object instance) {
            var fields = instance.GetType()
                .GetFields(COPYABLE)
                .Where(field => !field.IsDefined(typeof(NonSerializedAttribute), true));
            foreach(FieldInfo field in fields) {
                info.AddValue(field.Name, field.GetValue(instance), field.FieldType);
            }
        }

        /// <summary>
        /// warning: structs should make use of the return value.
        /// </summary>
        public static object SetObjectFields(this SerializationInfo info, object instance) {
            foreach(SerializationEntry item in info) {
                FieldInfo field = instance.GetType().GetField(item.Name, COPYABLE);
                if(field != null) {
                    object val = Convert.ChangeType(item.Value, field.FieldType);
                    field.SetValue(instance, val);
                }
            }
            return instance;
        }
    }
}
