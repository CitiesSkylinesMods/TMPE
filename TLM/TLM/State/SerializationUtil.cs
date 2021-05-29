namespace TrafficManager.State {
    using System.IO;
    using System.Runtime.Serialization.Formatters;
    using System.Runtime.Serialization.Formatters.Binary;

    public static class SerializationUtil {
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
    }
}
