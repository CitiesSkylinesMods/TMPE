using System;
using System.IO;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

namespace TrafficManager.Util.Record {
    public static class RecordUtil {
        static BinaryFormatter GetBinaryFormatter =>
            new BinaryFormatter { AssemblyFormat = FormatterAssemblyStyle.Simple };

        public static IRecordable Deserialize(byte[] data) {
            if (data == null)
                return null;
            //Log.Debug($"SerializationUtil.Deserialize(data): data.Length={data?.Length}");

            var memoryStream = new MemoryStream();
            memoryStream.Write(data, 0, data.Length);
            memoryStream.Position = 0;
            return GetBinaryFormatter.Deserialize(memoryStream) as IRecordable;
        }

        public static byte[] Serialize(IRecordable obj) {
            var memoryStream = new MemoryStream();
            GetBinaryFormatter.Serialize(memoryStream, obj);
            memoryStream.Position = 0; // redundant
            return memoryStream.ToArray();
        }
    }
}
