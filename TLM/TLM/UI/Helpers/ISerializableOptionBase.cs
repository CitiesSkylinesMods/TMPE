namespace TrafficManager.UI.Helpers {
    public interface ISerializableOptionBase
    {
        public void Load(byte data);
        public byte Save();
    }
}
