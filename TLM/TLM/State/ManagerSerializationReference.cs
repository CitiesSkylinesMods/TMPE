using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace TrafficManager.State {
    internal class ManagerSerializationReference : IComparable<ManagerSerializationReference> {

        public Type ManagerType { get; set; }

        public Type ContainerType { get; set; }

        public Func<IEnumerable<Type>> GetDependencies { get; set; }

        public Func<object, bool> LoadData { get; set; }

        public Func<object> SaveData { get; set; }

        public static ManagerSerializationReference ForManager(object manager) {

            var genericType = manager.GetType()
                                        .GetInterfaces()
                                        .SingleOrDefault(i => i.IsGenericType
                                                                && i.GetGenericTypeDefinition() == typeof(IManagerSerialization<>));

            if (genericType != null) {
                var getSerializationDependencies = genericType.GetMethod("GetSerializationDependencies");
                var loadData = genericType.GetMethod("LoadData");
                var saveData = genericType.GetMethod("SaveData");
                return new ManagerSerializationReference {
                    ManagerType = manager.GetType(),
                    ContainerType = genericType.GetGenericArguments()[0],
                    GetDependencies = () => (IEnumerable<Type>)getSerializationDependencies.Invoke(manager, null),
                    LoadData = data => (bool)loadData.Invoke(manager, new[] { data }),
                    SaveData = () => saveData.Invoke(manager, null),
                };
            }
            return null;
        }

        public int CompareTo(ManagerSerializationReference other) {
            var referencesOther = GetDependencies()?.Any(t => t.IsAssignableFrom(other.ManagerType)) ?? false;
            var referencedByOther = other.GetDependencies()?.Any(t => t.IsAssignableFrom(ManagerType)) ?? false;
            return referencesOther
                    ? referencedByOther ? 0 : 1
                    : referencedByOther ? -1 : 0;
        }
    }
}
