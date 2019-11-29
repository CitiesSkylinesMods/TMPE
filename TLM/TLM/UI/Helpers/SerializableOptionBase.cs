//TODO remove file
namespace TrafficManager.UI.Helpers {
    using System;
    using System.Runtime.Serialization;

    //TODO issue #562: Inherit ISerializable Interface or something.
    //[Serializable()]
    public abstract class SerializableOptionBase<TVal>: ISerializableOptionBase
    {
        public abstract void Load(byte data);
        public abstract byte Save();

        protected TVal _value;
        public readonly TVal DefaultValue;
        public TVal Value {
            get => _value;
            set => _value = value;
        }
        protected SerializableOptionBase(TVal defaultValue) => _value = DefaultValue = defaultValue;
    }
}
