using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TrafficManager.State {
    internal interface IManagerSerialization<T> {

        IEnumerable<Type> GetSerializationDependencies();

        bool LoadData(T data);

        T SaveData(ref bool success);
    }
}
