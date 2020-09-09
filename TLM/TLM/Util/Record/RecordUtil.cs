namespace TrafficManager.Util.Record {
    using TrafficManager.State;
    using System;

    [Obsolete("kept for Backward compatibility with MoveIT. to be deleted before release 11.6")]
    public static class RecordUtil {
        public static IRecordable Deserialize(byte[] data) =>
            SerializationUtil.Deserialize(data) as IRecordable;

        public static byte[] Serialize(IRecordable obj) =>
            SerializationUtil.Serialize(obj);
    }
}
