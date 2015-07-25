using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using ICities;

namespace TrafficManager
{
    class SelfDestruct
    {
        private static bool _selfDestructExecuted = false;

        public static void SelfDestructCall(ILoadingExtension currentInstance)
        {
            if (currentInstance == null || _selfDestructExecuted) return;

            _selfDestructExecuted = true;
            currentInstance.OnLevelUnloading();
            currentInstance.OnReleased();
        }

        public static void DestructOldInstances(ILoadingExtension currentInstance)
        {
            var targetType = typeof (SelfDestruct);
            var currentAssemblyName = Assembly.GetAssembly(targetType).GetName().Name;
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(
                a => a.GetName().Name == currentAssemblyName);

            foreach (var selfDestructMethod in from assembly in assemblies
                        select assembly.GetTypes().FirstOrDefault(t => t.Name == targetType.Name) 
                        into oldInstance where oldInstance != null && oldInstance != targetType
                            select oldInstance.GetMethod("SelfDestructCall"))
            {
                selfDestructMethod?.Invoke(null, new object[] {currentInstance});
            }
        }
    }
}
