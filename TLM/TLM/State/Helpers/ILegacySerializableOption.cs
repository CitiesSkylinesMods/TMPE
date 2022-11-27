namespace TrafficManager.State.Helpers {

    //legacy load and save
    public interface ILegacySerializableOption
    {
        void ResetValue();
        void Load(byte data); // TODO keep this for backward compatibility.
        byte Save(); // TODO: delete this once xml serialization is ready
    }
}
