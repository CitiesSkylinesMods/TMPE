namespace TrafficManager.UI.Helpers {

    //legacy load and save
    internal interface ILegacySerializableOption
    {
        public void Load(byte data); // TODO keep this for backward compatibality.
        public byte Save(); // TODO: delete this once xml serialization is ready
    }
}
