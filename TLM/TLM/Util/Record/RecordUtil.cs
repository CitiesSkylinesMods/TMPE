namespace TrafficManager.Util.Record {
    using TrafficManager.State;

    // This API is used by MoveIT mod.
    public static class RecordUtil {
        public static IRecordable Deserialize(byte[] data) =>
            SerializationUtil.Deserialize(data) as IRecordable;

        public static byte[] Serialize(IRecordable obj) =>
            SerializationUtil.Serialize(obj);
    }
}
