using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace TrafficManager.State {
    internal interface IPersistentObject {

        Type DependencyTarget { get; }

        IEnumerable<Type> GetDependencies();

        string ElementName { get; }

        bool CanLoad(XElement element);

        PersistenceResult LoadData(XElement element, PersistenceContext context);

        PersistenceResult SaveData(XElement container, PersistenceContext context);
    }
}