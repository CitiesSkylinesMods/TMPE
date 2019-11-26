namespace TrafficManager.UI.Helpers {
    public abstract class SerializableOptionBase {
        public abstract void Load(byte data);
        public abstract byte Save();
    }
}
